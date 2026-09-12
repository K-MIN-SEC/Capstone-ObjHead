# Object Head Battle - Nakama

This folder keeps local development and remote deployment on the same Docker Compose stack.

## Local development

1. Install Docker Desktop.
2. Copy `.env.example` to `.env` and choose local secrets.
3. Run `docker compose up -d` in this folder.
4. Nakama listens on `127.0.0.1:7350`; the console is at `127.0.0.1:7351`.
5. In Unity press F8, enter the same server key, then connect.

Run two clients on one PC with different Test Profile values such as `A` and `B`.

## Remote demo server

Copy this folder to a Linux VPS with Docker, set strong `.env` values, and put TLS/reverse-proxy configuration in front of port 7350. Configure the Unity panel/profile with the public hostname and `https` scheme. Do not expose PostgreSQL publicly.

## Current scope

- Nakama device-independent test authentication
- Relayed room creation and match-ID join
- Two-player quick matchmaking
- Host-owned lobby state and ready checks
- Fixed or random map selection encoded into `GameStartData`
- Reserved opcodes for gameplay commands, authoritative events, terrain operations, and snapshots

The relay host is temporary demo authority. Turn validation, health, inventory, win state, and terrain operations can move behind an authoritative Nakama match handler without changing the lobby contract.

## Editor-owned configuration

Edit `Assets/Resources/ObjectHeadNetworkConfig.asset` in the Unity Inspector for server profiles, map selection defaults, and the temporary panel layout. Do not put production secrets in Git.

For world placement, add `ObjectHeadMapAuthoring` to a scene object and assign movable child transforms for the terrain origin, water surface, and each player spawn range. Scene markers override compatibility defaults in `ObjectHeadMatchBootstrap`.
