-- Transport limits are server settings. Content rules are exported by the Unity build.
local catalog = require("authority_catalog")
return {
  protocol = 3,
  ruleset = catalog.ruleset,
  tick_rate = 20,
  empty_timeout_seconds = 60,
  worker_timeout_seconds = 15,
  loading_timeout_seconds = 45,
  max_room_seconds = 7200,
  max_payload_bytes = 16384,
  commands_per_tick = 8,
  password_min_length = 4,
  password_max_length = 32,
  password_attempts = 5,
  password_window_seconds = 60,
  admission_ticket_seconds = 30,
  characters = catalog.characters,
  common_slots = catalog.common_slots,
  maps = catalog.maps,
  modes = catalog.modes
}
