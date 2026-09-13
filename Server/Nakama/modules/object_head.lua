local nk = require("nakama")

local ROOM_CODE_COLLECTION = "object_head_room_codes"
local SYSTEM_USER_ID = "00000000-0000-0000-0000-000000000000"
local ROOM_CODE_LIFETIME_SECONDS = 21600

local function normalize_code(value)
  if value == nil then
    return ""
  end

  return string.upper(string.gsub(tostring(value), "[^A-Za-z0-9]", ""))
end

local function read_mapping(code)
  local objects = nk.storage_read({{
    collection = ROOM_CODE_COLLECTION,
    key = code,
    user_id = SYSTEM_USER_ID
  }})

  if objects == nil or #objects == 0 then
    return nil
  end

  return objects[1].value
end

local function register_room_code(context, payload)
  local request = nk.json_decode(payload or "{}")
  local match_id = request.match_id
  if match_id == nil or match_id == "" then
    error("match_id is required")
  end

  local code = nil
  for _ = 1, 12 do
    local candidate = string.upper(string.sub(string.gsub(nk.uuid_v4(), "-", ""), 1, 6))
    if read_mapping(candidate) == nil then
      code = candidate
      break
    end
  end

  if code == nil then
    error("could not allocate a room code")
  end

  local now = os.time()
  nk.storage_write({{
    collection = ROOM_CODE_COLLECTION,
    key = code,
    user_id = SYSTEM_USER_ID,
    value = {
      match_id = match_id,
      created_by = context.user_id,
      expires_at = now + ROOM_CODE_LIFETIME_SECONDS
    },
    permission_read = 0,
    permission_write = 0
  }})

  return nk.json_encode({ room_code = code, match_id = match_id })
end

local function resolve_room_code(_, payload)
  local request = nk.json_decode(payload or "{}")
  local code = normalize_code(request.room_code)
  if string.len(code) ~= 6 then
    error("room code must contain 6 letters or numbers")
  end

  local mapping = read_mapping(code)
  if mapping == nil then
    error("room code was not found")
  end

  if mapping.expires_at ~= nil and mapping.expires_at < os.time() then
    error("room code has expired")
  end

  return nk.json_encode({ room_code = code, match_id = mapping.match_id })
end

nk.register_rpc(register_room_code, "objecthead_register_room_code")
nk.register_rpc(resolve_room_code, "objecthead_resolve_room_code")
nk.logger_info("Object Head room-code RPCs loaded")
