local nk = require("nakama")
local rules = require("authority_rules")
local M = {}

local function decode(raw)
  if type(raw) ~= "string" or #raw > rules.max_payload_bytes then return nil end
  local ok, value = pcall(nk.json_decode, raw)
  if ok and type(value) == "table" then return value end
end
local function integer(n) return type(n)=="number" and n==math.floor(n) and math.abs(n)<2147483647 end
local function finite(n) return type(n)=="number" and n==n and math.abs(n)<1000000 end
local function contains(values, value)
  for _,v in ipairs(values) do if value==v then return true end end
  return false
end
function M.settings(input)
  if type(input)~="table" or not rules.modes[input.mode] then return nil end
  if input.rulesetVersion~=rules.ruleset then return nil end
  if not contains(rules.maps,input.fixedMapId) then return nil end
  if input.mapSelectionMode~=0 and input.mapSelectionMode~=1 then return nil end
  local pool={}
  for _,id in ipairs(type(input.randomMapPool)=="table" and input.randomMapPool or {}) do
    if not contains(rules.maps,id) or contains(pool,id) then return nil end
    pool[#pool+1]=id
  end
  if input.mapSelectionMode==1 and #pool==0 then return nil end
  local count=rules.modes[input.mode].players
  return {mode=input.mode,rulesetVersion=rules.ruleset,minPlayers=count,maxPlayers=count,
    fixedMapId=input.fixedMapId,mapSelectionMode=input.mapSelectionMode,randomMapPool=pool}
end
local function selection(state, list)
  if type(list)~="table" or #list~=rules.modes[state.settings.mode].characters then return false end
  for _,id in ipairs(list) do if not integer(id) or not rules.characters[id] then return false end end
  return true
end
local function ordered(state)
  local result={}
  for _,p in pairs(state.players) do result[#result+1]=p end
  table.sort(result,function(a,b) return a.playerIndex<b.playerIndex end)
  return result
end
local function label(state)
  return nk.json_encode({game="object_head_authority",ruleset=rules.ruleset,roomCode=state.code,
    mode=state.settings.mode,phase=state.phase,public=state.public,players=#ordered(state),
    capacity=state.settings.maxPlayers,mapId=state.settings.fixedMapId,worker=state.worker~=nil})
end
local function send(dispatcher,op,data,targets,sender)
  local json=nk.json_encode(data)
  for _,field in ipairs({"players","characters","randomMapPool"}) do json=json:gsub('"'..field..'":{}','"'..field..'":[]') end
  dispatcher.broadcast_message(op,json,targets,sender,true)
end
local function lobby(state,dispatcher)
  state.revision=state.revision+1
  dispatcher.match_label_update(label(state))
  send(dispatcher,2,{protocolVersion=rules.protocol,revision=state.revision,matchId=state.id,
    roomCode=state.code,hostUserId=state.owner,settings=state.settings,players=ordered(state),
    dedicatedAuthority=true,authorityReady=state.worker~=nil})
end
local function failure(dispatcher,presence,reason)
  send(dispatcher,8,{reason=reason},{presence})
end
function M.match_init(ctx,params)
  local settings=M.settings(params.settings)
  if not settings then error("invalid_settings") end
  local state={id=ctx.match_id,owner=params.owner,code=params.code,public=params.public==true,
    settings=settings,players={},presences={},reservations={},phase="lobby",revision=0,
    created=nk.time(),empty_since=0,worker_tick=0,last_sequence=0,expected=params.expected}
  return state,rules.tick_rate,label(state)
end
function M.match_join_attempt(ctx,dispatcher,tick,state,presence,metadata)
  if state.worker_id==presence.user_id and metadata and metadata.workerTicket==state.worker_ticket then
    if state.worker then return state,false,"worker_already_connected" end
    return state,true
  end
  if presence.user_id==state.worker_id then return state,false,"invalid_worker_ticket" end
  if state.phase~="lobby" then return state,false,"match_in_progress" end
  if state.expected and not state.expected[presence.user_id] then return state,false,"reserved_match" end
  if state.players[presence.user_id] or state.reservations[presence.user_id] then return state,false,"duplicate_player" end
  local occupied=0
  for _ in pairs(state.players) do occupied=occupied+1 end
  for _,at in pairs(state.reservations) do if tick-at<rules.tick_rate*10 then occupied=occupied+1 end end
  if occupied>=state.settings.maxPlayers then return state,false,"room_full" end
  state.reservations[presence.user_id]=tick
  return state,true
end
function M.match_join(ctx,dispatcher,tick,state,presences)
  for _,p in ipairs(presences) do
    if p.user_id==state.worker_id then state.worker=p;state.worker_tick=tick;state.worker_ticket=nil
    else
      local used={}
      for _,player in pairs(state.players) do used[player.playerIndex]=true end
      local seat=1;while used[seat] do seat=seat+1 end
      state.players[p.user_id]={userId=p.user_id,username=p.username,playerIndex=seat,ready=false,characters={}}
      state.presences[p.user_id]=p;state.reservations[p.user_id]=nil
    end
  end
  lobby(state,dispatcher)
  return state
end
function M.match_leave(ctx,dispatcher,tick,state,presences)
  for _,p in ipairs(presences) do
    if state.worker and p.session_id==state.worker.session_id then
      state.worker=nil
      if state.phase~="lobby" then send(dispatcher,9,{reason="combat_server_lost"});return nil end
    elseif state.presences[p.user_id] and state.presences[p.user_id].session_id==p.session_id then
      state.players[p.user_id]=nil;state.presences[p.user_id]=nil
      if state.phase~="lobby" then send(dispatcher,9,{reason="player_disconnected"});return nil end
    end
  end
  if not state.players[state.owner] then local players=ordered(state);state.owner=players[1] and players[1].userId or "" end
  lobby(state,dispatcher)
  return state
end
local function start_game(state,dispatcher,presence)
  if presence.user_id~=state.owner then return failure(dispatcher,presence,"host_only") end
  if not state.worker then return failure(dispatcher,presence,"combat_server_unavailable") end
  local players=ordered(state)
  if #players~=state.settings.maxPlayers then return failure(dispatcher,presence,"players_required") end
  for i,p in ipairs(players) do
    if not p.ready or not selection(state,p.characters) then return failure(dispatcher,presence,"selection_required") end
    p.playerIndex=i;p.allianceId=state.settings.mode==2 and ((i-1)%2+1) or i
  end
  local seed=tonumber(string.sub(string.gsub(nk.uuid_v4(),"-",""),1,7),16)
  local map=state.settings.fixedMapId
  if state.settings.mapSelectionMode==1 then map=state.settings.randomMapPool[seed % #state.settings.randomMapPool+1] end
  state.phase="loading";state.last_sequence=0;state.loaded={};state.worker_loaded=false;state.loading_started=nk.time()
  state.start={protocolVersion=rules.protocol,matchId=state.id,dedicatedAuthority=true,authorityUserId=state.worker_id,
    rulesetVersion=rules.ruleset,mode=state.settings.mode,playerCount=#players,mapSelectionMode=state.settings.mapSelectionMode,
    mapId=map,mapSeed=seed,characterSpawnSeed=seed,startingPlayerIndex=seed%#players+1,
    startedAtUnixMilliseconds=nk.time()/1000,players=players}
  dispatcher.match_label_update(label(state))
  send(dispatcher,5,state.start)
end
local function try_begin_battle(state,dispatcher)
  if state.phase~="loading" or not state.worker_loaded then return end
  for id in pairs(state.players) do if not state.loaded[id] then return end end
  state.phase="playing";dispatcher.match_label_update(label(state))
  send(dispatcher,123,{protocolVersion=rules.protocol})
end
local function player_message(state,dispatcher,tick,presence,op,data)
  local player=state.players[presence.user_id]
  if not player then return end
  if op==121 and state.phase=="loading" then
    state.loaded[presence.user_id]=true;try_begin_battle(state,dispatcher);return
  end
  if op==7 and presence.user_id==state.owner and state.phase=="playing" and state.turn and state.turn.matchOver then
    state.phase="lobby";state.turn=nil;state.last_sequence=0
    for _,p in pairs(state.players) do p.ready=false end
    send(dispatcher,7,{protocolVersion=rules.protocol});lobby(state,dispatcher);return
  end
  if state.phase=="lobby" then
    if op==1 then
      if type(data.username)=="string" and #data.username>0 and #data.username<=64 then player.username=data.username end
      lobby(state,dispatcher)
    elseif op==6 and selection(state,data.characters) then
      player.characters=data.characters;player.ready=false;lobby(state,dispatcher)
    elseif op==3 and type(data.ready)=="boolean" and (not data.ready or selection(state,player.characters)) then
      player.ready=data.ready;lobby(state,dispatcher)
    elseif op==4 and presence.user_id==state.owner then
      local next_settings=M.settings(data.settings)
      if next_settings and #ordered(state)<=next_settings.maxPlayers then
        state.settings=next_settings
        for _,p in pairs(state.players) do p.ready=false;if not selection(state,p.characters) then p.characters={} end end
        lobby(state,dispatcher)
      end
    elseif op==5 then start_game(state,dispatcher,presence) end
    return
  end
  if state.phase~="playing" or op~=100 or not state.turn or not state.worker then return end
  if data.turnSerial~=state.turn.turnSerial or player.playerIndex~=state.turn.currentPlayerIndex then return end
  if type(data.messageId)~="string" or #data.messageId>128 then return end
  if data.kind~=4 and data.characterId~=state.turn.characterId then return end
  if data.kind==2 or data.kind==5 then
    if state.turn.actionUsed or not finite(data.normalizedPower) or data.normalizedPower<0 or data.normalizedPower>1 then return end
    if not finite(data.aimX) or not finite(data.aimY) or data.aimX*data.aimX+data.aimY*data.aimY<0.0001 then return end
    if data.kind==2 and (not integer(data.selectedSkillIndex) or data.selectedSkillIndex<0 or data.selectedSkillIndex>2) then return end
    if data.kind==5 and (not integer(data.commonSlot) or data.commonSlot<0 or data.commonSlot>2) then return end
  elseif data.kind==7 then
    if not finite(data.moveX) or math.abs(data.moveX)>1 or type(data.jumpHeld)~="boolean" then return end
    if data.jumpPressed~=nil and type(data.jumpPressed)~="boolean" then return end
  elseif data.kind~=4 then return end
  -- Preserve only the verified sender. Clients never publish snapshots, HP or terrain.
  send(dispatcher,100,data,{state.worker},presence)
end
function M.match_loop(ctx,dispatcher,tick,state,messages)
  if state.phase=="loading" and nk.time()-state.loading_started>rules.loading_timeout_seconds*1000000 then
    send(dispatcher,9,{reason="loading_timeout"});return nil
  end
  if #ordered(state)==0 then
    state.empty_since=state.empty_since or tick
    if tick-state.empty_since>rules.empty_timeout_seconds*rules.tick_rate then send(dispatcher,9,{reason="room_empty"});return nil end
  else state.empty_since=nil end
  if nk.time()-state.created>rules.max_room_seconds*1000000 then send(dispatcher,9,{reason="session_expired"});return nil end
  if state.worker and tick-state.worker_tick>rules.worker_timeout_seconds*rules.tick_rate then
    send(dispatcher,9,{reason="combat_server_timeout"});return nil
  end
  for id,at in pairs(state.reservations) do if tick-at>=rules.tick_rate*10 then state.reservations[id]=nil end end
  local rates={}
  for _,message in ipairs(messages) do
    local p=message.sender
    local worker=state.worker and p.session_id==state.worker.session_id
    rates[p.session_id]=(rates[p.session_id] or 0)+1
    local data=decode(message.data)
    if data and data.protocolVersion==rules.protocol and (worker or rates[p.session_id]<=rules.commands_per_tick) then
      if worker then
        state.worker_tick=tick
        if message.op_code==121 and state.phase=="loading" then state.worker_loaded=true;try_begin_battle(state,dispatcher) end
        if state.phase=="playing" and (message.op_code==101 or message.op_code==110 or message.op_code==120) then
          if message.op_code==120 and data.kind==3 then
            if integer(data.stateSequence) and data.stateSequence>state.last_sequence then
              state.last_sequence=data.stateSequence;state.turn=data
              send(dispatcher,120,data,nil,nil)
            end
          elseif message.op_code~=120 then send(dispatcher,message.op_code,data,nil,nil) end
        end
      elseif state.presences[p.user_id] and state.presences[p.user_id].session_id==p.session_id then
        player_message(state,dispatcher,tick,p,message.op_code,data)
      end
    end
  end
  return state
end
function M.match_signal(ctx,dispatcher,tick,state,raw)
  local data=decode(raw)
  if data and data.claimWorker and state.phase=="lobby" and not state.worker and not state.players[data.userId] then
    if state.worker_id and tick<(state.claim_tick or 0)+rules.tick_rate*30 then return state,nk.json_encode({error="worker_reserved"}) end
    state.worker_id=data.userId;state.worker_ticket=nk.uuid_v4();state.claim_tick=tick
    return state,nk.json_encode({matchId=state.id,workerTicket=state.worker_ticket})
  end
  return state,nk.json_encode({error="worker_unavailable"})
end
function M.match_terminate(ctx,dispatcher,tick,state,grace)
  send(dispatcher,9,{reason="server_shutdown"})
  return state
end
return M
