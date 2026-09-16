-- Server-owned admission rules. Version this together with the Unity content catalog.
return {
  protocol = 3,
  ruleset = "authority-0916-v1",
  tick_rate = 20,
  empty_timeout_seconds = 60,
  worker_timeout_seconds = 15,
  max_room_seconds = 7200,
  max_payload_bytes = 16384,
  commands_per_tick = 8,
  characters = { [0]=true, [1]=true, [2]=true, [3]=true, [4]=true, [5]=true },
  maps = { "wind_meadow", "twin_citadels", "shattered_reef" },
  modes = {
    [0] = { players=2, characters=3 },
    [1] = { players=4, characters=1 },
    [2] = { players=4, characters=1 }
  }
}
