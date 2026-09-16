local nk=require("nakama")
local rules=require("authority_rules")
local match=require("authority_match")
local function request(ctx,payload)
  if not ctx.user_id then error("authentication_required") end
  if type(payload)~="string" or #payload>rules.max_payload_bytes then error("invalid_request") end
  local ok,data=pcall(nk.json_decode,payload)
  if not ok or type(data)~="table" then error("invalid_request") end
  return data
end
local function list(query)
  local result={}
  for _,entry in ipairs(nk.match_list(100,true,nil,nil,nil,"+label.game:object_head_authority +label.phase:lobby")) do
    local info=nk.json_decode(entry.label)
    if info.ruleset==rules.ruleset then result[#result+1]=entry end
  end
  return result
end
local function create(ctx,payload)
  local data=request(ctx,payload)
  local settings=match.settings(data.settings)
  if not settings then error("invalid_settings") end
  -- A transactional write bounds creation per account even across concurrent requests.
  local key={collection="object_head_room_limit",key="last_create",user_id=ctx.user_id}
  local old=nk.storage_read({key})[1]
  if old and nk.time()-old.value.at<3000000 then error("room_creation_rate_limited") end
  key.value={at=nk.time()};key.version=old and old.version or "*";key.permission_read=0;key.permission_write=0
  nk.storage_write({key})
  local code=string.upper(string.sub(string.gsub(nk.uuid_v4(),"-",""),1,6))
  -- Reserve the code atomically; never overwrite another room's mapping.
  local mapping={collection="object_head_authority_codes",key=code,user_id="00000000-0000-0000-0000-000000000000",
    value={pending=true},version="*",permission_read=0,permission_write=0}
  nk.storage_write({mapping})
  local id=nk.match_create("authority_match",{owner=ctx.user_id,settings=settings,code=code,public=data.public~=false})
  mapping.version=nil;mapping.value={matchId=id};nk.storage_write({mapping})
  return nk.json_encode({matchId=id,roomCode=code})
end
local function find(ctx,payload)
  local data=request(ctx,payload)
  if data.roomCode then
    if type(data.roomCode)~="string" then error("invalid_room_code") end
    local code=string.upper(data.roomCode):match("^%s*(%w%w%w%w%w%w)%s*$")
    if not code then error("invalid_room_code") end
    local entry=nk.storage_read({{collection="object_head_authority_codes",key=code,user_id="00000000-0000-0000-0000-000000000000"}})[1]
    if not entry or not entry.value.matchId or not nk.match_get(entry.value.matchId) then error("room_not_found") end
    return nk.json_encode({matchId=entry.value.matchId,roomCode=code})
  end
  local rooms={}
  for _,m in ipairs(list("+label.public:true +label.phase:lobby")) do
    local info=nk.json_decode(m.label)
    if info.public==true and info.players<info.capacity then info.matchId=m.match_id;rooms[#rooms+1]=info end
  end
  if #rooms==0 then return '{"rooms":[]}' end
  return nk.json_encode({rooms=rooms})
end
local function worker(ctx,payload)
  local data=request(ctx,payload)
  local secret=ctx.env and ctx.env.OBJECT_HEAD_WORKER_KEY
  if not secret or #secret<32 or type(data.key)~="string" or data.key~=secret then error("worker_authentication_failed") end
  if type(data.matchId)=="string" and #data.matchId>0 and #data.matchId<100 then
    return nk.match_signal(data.matchId,nk.json_encode({claimWorker=true,userId=ctx.user_id}))
  end
  local rooms={}
  for _,m in ipairs(list()) do
    local info=nk.json_decode(m.label)
    if info.worker==false then rooms[#rooms+1]={matchId=m.match_id} end
  end
  if #rooms==0 then return '{"rooms":[]}' end
  return nk.json_encode({rooms=rooms})
end
nk.register_rpc(create,"objecthead_authority_create")
nk.register_rpc(find,"objecthead_authority_find")
nk.register_rpc(worker,"objecthead_authority_worker")
nk.register_matchmaker_matched(function(ctx,users)
  if not users[1] or users[1].properties.ruleset~=rules.ruleset then return nil end
  local modes={Duel=0,FreeForAll=1,Teams=2}
  local mode=modes[users[1].properties.mode]
  if not rules.modes[mode] or #users~=rules.modes[mode].players then error("invalid_matchmaking_mode") end
  local expected={};local owner=users[1].presence.user_id
  for _,user in ipairs(users) do
    if user.properties.ruleset~=rules.ruleset or modes[user.properties.mode]~=mode then error("incompatible_matchmaking") end
    expected[user.presence.user_id]=true
    if user.presence.user_id<owner then owner=user.presence.user_id end
  end
  local settings={mode=mode,rulesetVersion=rules.ruleset,mapSelectionMode=1,fixedMapId=rules.maps[1],randomMapPool=rules.maps}
  return nk.match_create("authority_match",{owner=owner,settings=settings,code="",public=false,expected=expected})
end)
