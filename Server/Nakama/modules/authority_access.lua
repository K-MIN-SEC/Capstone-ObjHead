-- Admission is independent of combat rules, room discovery and matchmaking.
-- Password verification runs in RPC workers, never inside the match tick loop.
local nk=require("nakama")
local rules=require("authority_rules")
local M={}
local system="00000000-0000-0000-0000-000000000000"
local function key(id) return {collection="object_head_room_access",key=id,user_id=system} end
function M.password_valid(value)
  return type(value)=="string" and #value>=rules.password_min_length and #value<=rules.password_max_length and value:match("^[!-~]+$")~=nil
end
function M.save(id,password_hash)
  local entry=key(id);entry.value={passwordHash=password_hash};entry.permission_read=0;entry.permission_write=0
  nk.storage_write({entry})
end
function M.remove(id,code)
  local entries={key(id)}
  if code and #code>0 then entries[#entries+1]={collection="object_head_authority_codes",key=code,user_id=system} end
  pcall(nk.storage_delete,entries)
end
function M.authorize(ctx,id,password)
  if type(id)~="string" or #id>100 or not nk.match_get(id) then error("room_not_found") end
  local credentials=nk.storage_read({key(id)})[1]
  if not credentials then error("room_not_found") end
  -- Atomic per-account limiter also bounds concurrent bcrypt work.
  local counter={collection="object_head_room_attempts",key="admission",user_id=ctx.user_id}
  local old=nk.storage_read({counter})[1];local now=nk.time()
  local value=old and old.value or {at=now,count=0}
  if now-value.at>=rules.password_window_seconds*1000000 then value={at=now,count=0} end
  if value.count>=rules.password_attempts then error("room_password_rate_limited") end
  value.count=value.count+1;counter.value=value;counter.version=old and old.version or "*"
  counter.permission_read=0;counter.permission_write=0
  local saved=pcall(nk.storage_write,{counter});if not saved then error("room_password_rate_limited") end
  if type(password)~="string" or password=="" then error("room_password_required") end
  if not M.password_valid(password) or not nk.bcrypt_compare(credentials.value.passwordHash,password) then error("room_password_incorrect") end
  return nk.match_signal(id,nk.json_encode({grantAdmission=true,userId=ctx.user_id,sessionId=ctx.session_id}))
end
return M
