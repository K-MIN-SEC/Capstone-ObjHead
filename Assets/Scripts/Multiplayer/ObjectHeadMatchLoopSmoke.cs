using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opt-in, observation-only offline readiness test. No forced kills, shortened
/// timers, teleports, modified balance or time acceleration are used.
/// Full-match timeout is inconclusive (exit 3), never a manufactured victory.
/// </summary>
public sealed class ObjectHeadMatchLoopSmoke : MonoBehaviour
{
    private TurnManager turns;
    private float deadline;
    private float nextHeartbeat;
    private float startedAt;
    private float lastTurnAt;
    private bool finished;
    private bool failed;
    private bool observing;
    private bool fullMatch;
    private bool gateReported;
    private bool diagnostics;
    private int minimumTurns;
    private int completedTurns;
    private int committedActions;
    private int currentSerial = -1;
    private int currentPlayer;
    private int currentActor;
    private int currentStartingHp;
    private int currentShots;
    private int earlyEndStacks;
    private float currentMoved;
    private float currentStartedAt;
    private float movedByInput;
    private Vector2 previousPosition;
    private string currentKind;
    private string folder;
    private readonly HashSet<int> activePlayers = new HashSet<int>();
    private readonly HashSet<int> activeActors = new HashSet<int>();
    private readonly Dictionary<int, float> diagnosticAt = new Dictionary<int, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Environment.GetCommandLineArgs().Contains("-objectHeadMatchLoopSmoke")) return;
        var root = new GameObject("MatchLoopSmoke");
        DontDestroyOnLoad(root);
        root.AddComponent<ObjectHeadMatchLoopSmoke>();
    }

    private static string Arg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
    }

    private static int IntArg(string key, int fallback, int min, int max) =>
        int.TryParse(Arg(key), out int value) ? Mathf.Clamp(value, min, max) : fallback;

    private void Awake()
    {
        startedAt = Time.realtimeSinceStartup;
        deadline = startedAt + IntArg("-objectHeadLoopTimeout", 180, 30, 900);
        fullMatch = Environment.GetCommandLineArgs().Contains("-objectHeadLoopFullMatch");
        diagnostics = Environment.GetCommandLineArgs().Contains("-objectHeadLoopDiagnostics");
        minimumTurns = IntArg("-objectHeadLoopTurns", 6, 2, 40);
        folder = Arg("-objectHeadCapture");
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        Application.runInBackground = true;
    }

    private void OnEnable()
    {
        Application.logMessageReceived += OnLog;
        SkillFireController.FireCommitted += OnSkillCommitted;
        SkillProjectile.LandingCommitted += OnLandingCommitted;
        ObjectHeadPresentation.ImpactPresented += OnImpactPresented;
        CommonHeadUseController.UseCommitted += OnCommonCommitted;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLog;
        SkillFireController.FireCommitted -= OnSkillCommitted;
        SkillProjectile.LandingCommitted -= OnLandingCommitted;
        ObjectHeadPresentation.ImpactPresented -= OnImpactPresented;
        CommonHeadUseController.UseCommitted -= OnCommonCommitted;
        UnsubscribeTurns();
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
    }

    private void OnSkillCommitted(SkillFireController fire, float power, Vector2 aim, int skill)
    {
        if (fire != null) RecordAction(fire.GetComponent<TurnCharacterController>(), "skill=" + skill);
        if (diagnostics && observing && fire != null)
            Debug.Log($"[MATCH_LOOP_FIRE] turn={turns?.TurnSerial} actor={fire.name} skill={skill} power={power:F4} aim={aim.ToString("F4")} actorPosition={fire.transform.position.ToString("F4")} aimOrigin={fire.GetComponent<AimController>().AimOrigin.ToString("F4")} launchSpeed={fire.LaunchSpeed:F4} launchOffset={fire.LaunchOffset:F4}");
    }

    private void OnLandingCommitted(string id, Vector2 position, bool discarded)
    {
        if (diagnostics && observing)
            Debug.Log($"[MATCH_LOOP_LANDING] turn={turns?.TurnSerial} projectile={id} position={position.ToString("F4")} discarded={discarded}");
    }

    private void OnImpactPresented(Vector2 position, float radius, float impulse)
    {
        // Presentation is also emitted offline; the network landing event is not.
        // Log each real chain blast separately, not as a claimed projectile contact.
        if (diagnostics && observing)
            Debug.Log($"[MATCH_LOOP_IMPACT] turn={turns?.TurnSerial} position={position.ToString("F4")} radius={radius:F4} impulse={impulse:F4}");
    }

    private void OnCommonCommitted(CommonHeadUseController use, int slot, CommonHeadType type,
        float power, Vector2 aim, Vector2 origin)
    {
        if (use != null) RecordAction(use.GetComponent<TurnCharacterController>(), "common=" + type);
        if (diagnostics && observing && use != null)
            Debug.Log($"[MATCH_LOOP_FIRE] turn={turns?.TurnSerial} actor={use.name} common={type} slot={slot} power={power:F4} aim={aim.ToString("F4")} actorPosition={use.transform.position.ToString("F4")} origin={origin.ToString("F4")}");
    }

    private void RecordAction(TurnCharacterController actor, string action)
    {
        if (!observing || turns == null || actor == null || actor != turns.CurrentCharacter) return;
        committedActions++;
        currentShots++;
        RecordActivity(actor);
        Debug.Log($"[MATCH_LOOP_ACTION] turn={turns.TurnSerial} player={currentPlayer} actor={currentKind} {action}");
    }

    private void RecordActivity(TurnCharacterController actor)
    {
        activeActors.Add(actor.GetInstanceID());
        activePlayers.Add(actor.GetComponent<ObjectHeadTeamMember>().PlayerIndex);
    }

    private void Update()
    {
        if (finished) return;
        if (failed) { Finish(2, "[MATCH_LOOP_FAIL] runtime exception/error or failed readiness check"); return; }
        float now = Time.realtimeSinceStartup;
        if (observing && turns != null)
        {
            TurnCharacterController actor = turns.CurrentCharacter;
            if (actor != null && actor.GetInstanceID() == currentActor)
            {
                Vector2 position = actor.transform.position;
                if (Mathf.Abs(actor.InputMoveX) > .05f)
                {
                    float distance = Mathf.Abs(position.x - previousPosition.x);
                    currentMoved += distance;
                    movedByInput += distance;
                    if (distance > .001f) RecordActivity(actor);
                }
                previousPosition = position;
            }
            if (now >= nextHeartbeat)
            {
                nextHeartbeat = now + 15;
                Debug.Log($"[MATCH_LOOP_PROGRESS] elapsed={now-startedAt:F1}s turns={completedTurns} serial={turns.TurnSerial} phase={turns.CurrentPhase} actions={committedActions} moved={movedByInput:F2} alive={AliveCount()} hp={TotalHp()} aiActions={ObjectHeadAIDirector.ActionsTaken} jumps={ObjectHeadAIDirector.JumpsRequested} candidates={ObjectHeadAIDirector.CandidatesEvaluated}");
            }
            // An ordinary turn is 30 seconds; allow generous projectile/settlement time.
            if (!turns.IsMatchOver && now - lastTurnAt > 80)
            {
                Finish(2, $"[MATCH_LOOP_FAIL] no turn transition for 80s; serial={turns.TurnSerial} phase={turns.CurrentPhase} pending={turns.IsActionPending} remaining={turns.RemainingTurnSeconds:F1}");
                return;
            }
        }
        if (now > deadline)
        {
            string scope = fullMatch ? "natural full-match completion" : "consecutive-turn gate";
            Finish(3, $"[MATCH_LOOP_INCONCLUSIVE] timeout waiting for {scope}; completedTurns={completedTurns} actions={committedActions} moved={movedByInput:F2} alive={AliveCount()} hp={TotalHp()}; no victory/replay claim");
        }
    }

    private int AliveCount() => turns?.Characters?.Count(c => c != null && !c.GetComponent<CharacterCombat>().IsDead) ?? 0;
    private int TotalHp() => turns?.Characters?.Where(c => c != null).Sum(c => Math.Max(0, c.GetComponent<CharacterCombat>().CurrentHp)) ?? 0;

    private void OnTurnStarted(TurnCharacterController actor)
    {
        if (!observing || actor == null || turns.TurnSerial == currentSerial) return;
        currentSerial = turns.TurnSerial;
        currentPlayer = actor.GetComponent<ObjectHeadTeamMember>().PlayerIndex;
        currentActor = actor.GetInstanceID();
        currentKind = actor.GetComponent<DemoSkillSelector>().CharacterKind.ToString();
        currentStartingHp = TotalHp();
        currentShots = 0;
        currentMoved = 0;
        currentStartedAt = lastTurnAt = Time.realtimeSinceStartup;
        previousPosition = actor.transform.position;
        Debug.Log($"[MATCH_LOOP_TURN_START] serial={currentSerial} round={turns.RoundSerial} player={currentPlayer} actor={currentKind} position={previousPosition} alive={AliveCount()} hp={currentStartingHp}");
    }

    private void OnTurnEnded(TurnCharacterController actor)
    {
        if (!observing || actor == null || actor.GetInstanceID() != currentActor) return;
        completedTurns++;
        Debug.Log($"[MATCH_LOOP_TURN_END] serial={currentSerial} player={currentPlayer} actor={currentKind} seconds={Time.realtimeSinceStartup-currentStartedAt:F1} shots={currentShots} moved={currentMoved:F2} hpChange={TotalHp()-currentStartingHp} alive={AliveCount()}");
        if (diagnostics && currentShots == 0)
        {
            if (Time.realtimeSinceStartup - currentStartedAt < .15f && earlyEndStacks++ < 3)
                Debug.Log("[MATCH_LOOP_EARLY_END_STACK] " + Environment.StackTrace);
            DumpNoShotDiagnostics(actor);
        }
    }

    private void DumpNoShotDiagnostics(TurnCharacterController actor)
    {
        // Read-only, opt-in probes. Rate limiting avoids changing the AI's ordinary
        // planning budget through excessive physics queries or repeated log writes.
        int id = actor.GetInstanceID();
        float now = Time.realtimeSinceStartup;
        if (diagnosticAt.TryGetValue(id, out float previous) && now - previous < 45) return;
        diagnosticAt[id] = now;
        var terrain = FindAnyObjectByType<TerrainManager>();
        var fire = actor.GetComponent<SkillFireController>();
        var aim = actor.GetComponent<AimController>();
        var selector = actor.GetComponent<DemoSkillSelector>();
        var nav = new ObjectHeadAINavigation(actor, terrain);
        Vector2 center = actor.GetComponent<Collider2D>().bounds.center;
        bool jumpLeft = nav.TryJump(-1, out Vector2 leftLanding);
        bool jumpRight = nav.TryJump(1, out Vector2 rightLanding);
        int leftFloors = 0, rightFloors = 0;
        for (int i = 1; i <= 6; i++)
        {
            if (nav.GroundAt(center.x - 1.25f * i, center.y, out _)) leftFloors++;
            if (nav.GroundAt(center.x + 1.25f * i, center.y, out _)) rightFloors++;
        }
        Debug.Log($"[MATCH_LOOP_NO_SHOT] serial={currentSerial} player={currentPlayer} actor={currentKind} grounded={actor.IsGrounded} bodyClear={nav.Clear(center)} center={center} velocity={actor.GetComponent<Rigidbody2D>().linearVelocity} speed={fire.LaunchSpeed:F2} gravity={fire.ProjectileGravity:F2} canStartLeft={nav.CanStartToward(-1)} canStartRight={nav.CanStartToward(1)} jumpLeft={jumpLeft}:{leftLanding} jumpRight={jumpRight}:{rightLanding} groundProbesLeft={leftFloors} groundProbesRight={rightFloors} cooldowns={selector.GetRemainingCooldown(0)},{selector.GetRemainingCooldown(1)},{selector.GetRemainingCooldown(2)}");
        Debug.Log("[MATCH_LOOP_NO_SHOT_ROSTER] " + string.Join(";", turns.Characters.Where(c => c != null).Select(c =>
            $"P{c.GetComponent<ObjectHeadTeamMember>().PlayerIndex}:{c.GetComponent<DemoSkillSelector>().CharacterKind}:hp{c.GetComponent<CharacterCombat>().CurrentHp}:pos{c.transform.position}")));
        var target = turns.Characters.Where(c => c != null && c.GetComponent<CharacterCombat>().IsDead == false && !ObjectHeadAIPlanner.SameTeam(actor, c))
            .OrderBy(c => Vector2.Distance(center, c.transform.position)).FirstOrDefault();
        if (target == null) return;
        Vector2 targetPoint = target.GetComponent<Collider2D>().bounds.center;
        var planner = new ObjectHeadAIPlanner(actor, turns.Characters, terrain);
        for (int skill = 0; skill < 3; skill++)
        {
            var settings = selector.GetSkillSettings(skill);
            for (int arc = 0; arc < (settings.straightShot ? 1 : 2); arc++)
            {
                Vector2 direction = (targetPoint - aim.AimOrigin).normalized;
                bool reachable = true;
                for (int refine = 0; refine < 3; refine++)
                {
                    Vector2 delta = targetPoint - (aim.AimOrigin + direction * fire.LaunchOffset);
                    if (settings.straightShot) direction = delta.normalized;
                    else if (!ObjectHeadAIPlanner.SolveArc(delta, fire.LaunchSpeed, -Physics2D.gravity.y * fire.ProjectileGravity, arc == 1, out direction))
                    { reachable = false; break; }
                }
                Vector2 impact = default;
                bool hit = reachable && planner.Trace(settings, direction, 1, out impact);
                float score = hit ? planner.Score(settings, impact) : 0;
                Debug.Log($"[MATCH_LOOP_MAX_POWER_PROBE] serial={currentSerial} skill={skill} effect={settings.effectType} target={targetPoint} origin={aim.AimOrigin} highArc={arc==1} reachable={reachable} hit={hit} impact={impact} score={score:F2} blastRadius={settings.explosionRadiusWorld:F2} projectileSize={settings.projectileVisualDiameter:F2}");
            }
        }
    }

    private void UnsubscribeTurns()
    {
        if (turns == null) return;
        turns.TurnStarted -= OnTurnStarted;
        turns.TurnEnded -= OnTurnEnded;
    }

    private bool Check(bool condition, string label)
    {
        Debug.Log($"[MATCH_LOOP_CHECK] {label}={condition}");
        if (!condition) failed = true;
        return condition;
    }

    private void ReportTurnGate()
    {
        gateReported = true;
        Check(activePlayers.Count == 2, "both AI seats performed movement or an attack");
        Check(activeActors.Count >= Math.Min(3, minimumTurns), "multiple roster characters performed an action");
        Check(committedActions >= 2, "at least two real attacks committed");
        Debug.Log($"[MATCH_LOOP_TURNS_{(failed ? "FAIL" : "PASS")}] completed={completedTurns} players={activePlayers.Count} actors={activeActors.Count} attacks={committedActions} moved={movedByInput:F2} jumps={ObjectHeadAIDirector.JumpsRequested}; movement/jumps are observed, not forced; not a full-match result");
    }

    private IEnumerator Capture(string name)
    {
        if (!string.IsNullOrEmpty(folder) && Camera.main != null)
            yield return ObjectHeadReleaseSmoke.Capture(Path.Combine(folder, name + ".png"));
    }

    private IEnumerator Start()
    {
        yield return null;
        if (!Check(Mathf.Approximately(Time.timeScale, 1), "normal simulation time scale")) yield break;
        var catalog = ObjectHeadContent.Load();
        string requestedMap = Arg("-objectHeadLoopMap");
        var map = string.IsNullOrEmpty(requestedMap) ? catalog.maps.FirstOrDefault() : catalog.maps.FirstOrDefault(m => m.id == requestedMap);
        if (!Check(map != null, "requested map exists")) yield break;
        int seed = IntArg("-objectHeadLoopSeed", 160916, 1, int.MaxValue);
        string requestedDifficulty = Arg("-objectHeadLoopDifficulty");
        ObjectHeadAIDifficulty difficulty = ObjectHeadAIDifficulty.Pro;
        if (!string.IsNullOrEmpty(requestedDifficulty) &&
            (!Enum.TryParse(requestedDifficulty, true, out difficulty) || !Enum.IsDefined(typeof(ObjectHeadAIDifficulty), difficulty)))
        {
            Check(false, "requested AI difficulty exists: " + requestedDifficulty);
            yield break;
        }
        var roster = catalog.DefaultSelection(2);
        GameStartData.Apply(new GameStartData
        {
            localMatch = true, mode = ObjectHeadMatchMode.Duel, playerCount = 2,
            mapId = map.id, mapSeed = seed, characterSpawnSeed = seed, startingPlayerIndex = 1,
            players = Enumerable.Range(1, 2).Select(p => new ObjectHeadPlayerAssignment
            {
                playerIndex = p, allianceId = p, username = "AI " + p, isAi = true,
                aiDifficulty = difficulty, characters = roster.ToArray()
            }).ToArray()
        });
        Debug.Log($"[MATCH_LOOP_CONFIG] map={map.id} seed={seed} mode=offline_duel bothAI={difficulty} roster={string.Join(",", roster)} timeScale=1 goal={(fullMatch ? "natural_result_replay_title" : "consecutive_turns_only")} minimumTurns={minimumTurns}");
        SceneManager.LoadScene(map.sceneName);
        yield return null;
        float startupDeadline = Time.realtimeSinceStartup + 10;
        while (Time.realtimeSinceStartup < startupDeadline)
        {
            turns = FindAnyObjectByType<TurnManager>();
            if (turns != null && turns.CurrentCharacter != null) break;
            yield return null;
        }
        if (!Check(turns != null && turns.CurrentCharacter != null, "match starts naturally")) yield break;
        if (!Check(turns.Characters.Length == roster.Length * 2 && AliveCount() == roster.Length * 2, "complete living starting roster")) yield break;
        if (!Check(FindAnyObjectByType<ObjectHeadAIDirector>() != null, "AI director is active")) yield break;
        Debug.Log("[MATCH_LOOP_INITIAL] " + string.Join(";", turns.Characters.Select(c =>
            $"P{c.GetComponent<ObjectHeadTeamMember>().PlayerIndex}:{c.GetComponent<DemoSkillSelector>().CharacterKind}:hp{c.GetComponent<CharacterCombat>().CurrentHp}:pos{c.transform.position}")));
        observing = true;
        turns.TurnStarted += OnTurnStarted;
        turns.TurnEnded += OnTurnEnded;
        OnTurnStarted(turns.CurrentCharacter);
        yield return Capture("loop_initial");
        while (!finished && !turns.IsMatchOver)
        {
            if (!gateReported && completedTurns >= minimumTurns)
            {
                ReportTurnGate();
                yield return Capture("loop_turn_gate");
                if (!fullMatch)
                {
                    Finish(failed ? 2 : 0, failed ? "[MATCH_LOOP_FAIL] consecutive-turn readiness" : "[MATCH_LOOP_PASS] consecutive-turn gate only; natural victory/replay/title not tested");
                    yield break;
                }
            }
            yield return null;
        }
        if (finished) yield break;
        observing = false;
        UnsubscribeTurns();
        Debug.Log($"[MATCH_LOOP_NATURAL_RESULT] winner={turns.WinningPlayerIndex} turns={completedTurns} attacks={committedActions} alive={AliveCount()} hp={TotalHp()} seconds={Time.realtimeSinceStartup-startedAt:F1}");
        Check(committedActions > 0, "natural result followed actual combat");
        yield return new WaitForSecondsRealtime(.25f);
        var hud = FindAnyObjectByType<ObjectHeadBattleScreen>();
        if (!Check(hud != null && hud.resultPanel.activeInHierarchy && hud.replayButton.interactable, "natural result panel and replay button")) yield break;
        yield return Capture("loop_natural_result");
        int previousTurnsId = turns.GetInstanceID();
        hud.replayButton.onClick.Invoke();
        yield return null;
        startupDeadline = Time.realtimeSinceStartup + 10;
        while (Time.realtimeSinceStartup < startupDeadline)
        {
            turns = FindAnyObjectByType<TurnManager>();
            if (turns != null && turns.GetInstanceID() != previousTurnsId && turns.CurrentCharacter != null) break;
            yield return null;
        }
        if (!Check(turns != null && turns.GetInstanceID() != previousTurnsId && !turns.IsMatchOver && turns.CurrentCharacter != null && AliveCount() == roster.Length * 2, "replay creates a fresh ready match")) yield break;
        Check(SceneManager.GetActiveScene().name == map.sceneName && GameStartData.Instance.mapId == map.id, "replay retains the chosen map");
        yield return Capture("loop_replay");
        hud = FindAnyObjectByType<ObjectHeadBattleScreen>();
        yield return null;
        if (!Check(hud != null && hud.menuButton != null && hud.exitButton != null, "replayed match has navigation controls")) yield break;
        hud.menuButton.onClick.Invoke();
        Check(hud.menuPanel.activeSelf, "menu opens during replay");
        hud.exitButton.onClick.Invoke();
        float exitDeadline = Time.realtimeSinceStartup + 10;
        while (FindAnyObjectByType<ObjectHeadTitleScreen>() == null && Time.realtimeSinceStartup < exitDeadline) yield return null;
        Check(FindAnyObjectByType<ObjectHeadTitleScreen>() != null && GameStartData.Instance == null, "menu exit returns to title and clears match data");
        yield return Capture("loop_title_return");
        Finish(failed ? 2 : 0, failed ? "[MATCH_LOOP_FAIL] natural result/replay/title flow" : "[MATCH_LOOP_PASS] natural AI match result, real replay callback, menu exit to title; offline only");
    }

    private void Finish(int code, string message)
    {
        if (finished) return;
        finished = true;
        Debug.Log(message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
