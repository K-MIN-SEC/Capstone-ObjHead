using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadSpreadsheetImporter
{
    public const string WorkbookPath = "Assets/GameData/ObjectHeadData.xlsx";
    private const string LocalizationAssetPath = "Assets/Resources/ObjectHeadLocalization.asset";
    private const string BalanceAssetPath = "Assets/Resources/ObjectHeadBalance.asset";

    [MenuItem("Object Head/Data/Import ObjectHeadData.xlsx")]
    public static void Import()
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", WorkbookPath));
        if (!File.Exists(fullPath))
        {
            Debug.LogError("[Object Head] Data workbook was not found: " + WorkbookPath);
            return;
        }

        try
        {
            IReadOnlyList<Dictionary<string, string>> localizationRows = ObjectHeadXlsxReader.ReadRecords(fullPath, "번역");
            IReadOnlyList<Dictionary<string, string>> balanceRows = ObjectHeadXlsxReader.ReadRecords(fullPath, "수치");
            ImportLocalization(localizationRows);
            ImportBalance(balanceRows);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Object Head] Spreadsheet imported: {localizationRows.Count} translations, {balanceRows.Count} balance rows.");
        }
        catch (IOException exception)
        {
            Debug.LogError("[Object Head] Could not read ObjectHeadData.xlsx. Close Excel if the file is open, then import again.\n" + exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public static ObjectHeadLocalizationTable ImportAndGetLocalization()
    {
        Import();
        return AssetDatabase.LoadAssetAtPath<ObjectHeadLocalizationTable>(LocalizationAssetPath);
    }

    private static void ImportLocalization(IReadOnlyList<Dictionary<string, string>> rows)
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        List<ObjectHeadLocalizedEntry> entries = new List<ObjectHeadLocalizedEntry>();
        foreach (Dictionary<string, string> row in rows)
        {
            string key = Get(row, "key").Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!keys.Add(key))
            {
                throw new InvalidDataException("Duplicate localization key: " + key);
            }

            entries.Add(new ObjectHeadLocalizedEntry
            {
                key = key,
                korean = Get(row, "한국어"),
                english = Get(row, "English")
            });
        }

        ObjectHeadLocalizationTable table = AssetDatabase.LoadAssetAtPath<ObjectHeadLocalizationTable>(LocalizationAssetPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<ObjectHeadLocalizationTable>();
            AssetDatabase.CreateAsset(table, LocalizationAssetPath);
        }

        table.ConfigureForEditor(ObjectHeadLanguage.Korean, entries.ToArray());
        EditorUtility.SetDirty(table);
    }

    private static void ImportBalance(IReadOnlyList<Dictionary<string, string>> rows)
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        List<ObjectHeadBalanceEntry> entries = new List<ObjectHeadBalanceEntry>();
        foreach (Dictionary<string, string> row in rows)
        {
            string key = Get(row, "key").Trim();
            string rawValue = Get(row, "값").Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (!keys.Add(key))
            {
                throw new InvalidDataException("Duplicate balance key: " + key);
            }

            if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw new InvalidDataException($"Balance value for '{key}' is not a number: {rawValue}");
            }

            entries.Add(new ObjectHeadBalanceEntry
            {
                key = key,
                value = value,
                unit = Get(row, "단위"),
                description = Get(row, "설명")
            });
        }

        ObjectHeadBalanceTable table = AssetDatabase.LoadAssetAtPath<ObjectHeadBalanceTable>(BalanceAssetPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<ObjectHeadBalanceTable>();
            AssetDatabase.CreateAsset(table, BalanceAssetPath);
        }

        table.ConfigureForEditor(entries.ToArray());
        EditorUtility.SetDirty(table);
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }
}

public sealed class ObjectHeadSpreadsheetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets.Any(path => string.Equals(path, ObjectHeadSpreadsheetImporter.WorkbookPath, StringComparison.OrdinalIgnoreCase)))
        {
            EditorApplication.delayCall += ObjectHeadSpreadsheetImporter.Import;
        }
    }
}

internal static class ObjectHeadXlsxReader
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<Dictionary<string, string>> ReadRecords(string path, string sheetName)
    {
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            List<string> sharedStrings = ReadSharedStrings(archive);
            ZipArchiveEntry sheetEntry = ResolveSheetEntry(archive, sheetName);
            XDocument sheetDocument = LoadXml(sheetEntry);
            List<List<string>> rows = ReadRows(sheetDocument, sharedStrings);
            int headerIndex = rows.FindIndex(row => row.Any(value => string.Equals(value, "key", StringComparison.OrdinalIgnoreCase)));
            if (headerIndex < 0)
            {
                throw new InvalidDataException($"Sheet '{sheetName}' has no key header.");
            }

            List<string> headers = rows[headerIndex];
            List<Dictionary<string, string>> records = new List<Dictionary<string, string>>();
            for (int rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> values = rows[rowIndex];
                Dictionary<string, string> record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int column = 0; column < headers.Count; column++)
                {
                    string header = headers[column];
                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        record[header] = column < values.Count ? values[column] : string.Empty;
                    }
                }

                if (record.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    records.Add(record);
                }
            }

            return records;
        }
    }

    private static ZipArchiveEntry ResolveSheetEntry(ZipArchive archive, string sheetName)
    {
        XDocument workbook = LoadXml(RequireEntry(archive, "xl/workbook.xml"));
        XElement sheet = workbook.Descendants(SpreadsheetNamespace + "sheet")
            .FirstOrDefault(candidate => string.Equals((string)candidate.Attribute("name"), sheetName, StringComparison.Ordinal));
        if (sheet == null)
        {
            throw new InvalidDataException("Workbook has no sheet named: " + sheetName);
        }

        string relationshipId = (string)sheet.Attribute(RelationshipNamespace + "id");
        XDocument relationships = LoadXml(RequireEntry(archive, "xl/_rels/workbook.xml.rels"));
        XElement relationship = relationships.Descendants(PackageRelationshipNamespace + "Relationship")
            .FirstOrDefault(candidate => string.Equals((string)candidate.Attribute("Id"), relationshipId, StringComparison.Ordinal));
        if (relationship == null)
        {
            throw new InvalidDataException("Workbook relationship is missing for sheet: " + sheetName);
        }

        string target = ((string)relationship.Attribute("Target") ?? string.Empty).Replace('\\', '/');
        string entryPath = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target.TrimStart('/');
        return RequireEntry(archive, entryPath);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return new List<string>();
        }

        XDocument document = LoadXml(entry);
        return document.Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static List<List<string>> ReadRows(XDocument document, IReadOnlyList<string> sharedStrings)
    {
        List<List<string>> result = new List<List<string>>();
        foreach (XElement row in document.Descendants(SpreadsheetNamespace + "row"))
        {
            List<string> values = new List<string>();
            foreach (XElement cell in row.Elements(SpreadsheetNamespace + "c"))
            {
                int column = ParseColumnIndex((string)cell.Attribute("r"));
                while (values.Count <= column)
                {
                    values.Add(string.Empty);
                }

                values[column] = ReadCell(cell, sharedStrings);
            }

            result.Add(values);
        }

        return result;
    }

    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        string type = (string)cell.Attribute("t") ?? string.Empty;
        if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
        }

        string raw = (string)cell.Element(SpreadsheetNamespace + "v") ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.Ordinal) && int.TryParse(raw, out int index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return raw;
    }

    private static int ParseColumnIndex(string reference)
    {
        int result = 0;
        int length = 0;
        foreach (char character in reference ?? string.Empty)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            length++;
        }

        return length > 0 ? result - 1 : 0;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using (Stream stream = entry.Open())
        {
            return XDocument.Load(stream);
        }
    }

    private static ZipArchiveEntry RequireEntry(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.GetEntry(path);
        if (entry == null)
        {
            throw new InvalidDataException("XLSX entry is missing: " + path);
        }

        return entry;
    }
}
