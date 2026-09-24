# EOS host multiplayer

The game now uses Epic Online Services for Connect identity, lobby discovery,
room codes and reliable P2P messages. One player's game is the combat authority.
EOS does **not** run the battle simulation on a central game server. Solo AI and
training remain local. The old Nakama implementation is retained for migration,
but `ObjectHeadNetworkConfig.asset` selects EOS for the normal online UI.

## Epic development setup (2026-09-24)

The temporary EOS product `ObjectHead Dev` and its `ObjectHeadDevClient` are configured
in the `minsec` organization. The custom `ObjectHeadLobbyClient` policy requires a
signed-in user and grants only lobby create/join, read, and public-search actions.
The final store title has not been chosen; this EOS product is for development.

The Unity editor on the current workstation has local, git-ignored EOS JSON config
for the product's Live deployment. The client secret is deliberately not in this
document or source control. A fresh checkout or another build machine needs its
own local plugin configuration before building.

**Verified here:** Unity loaded the config, Device ID login succeeded, and EOS
public and password-protected 2-player lobbies were each created with a
six-character room code and then closed. A Windows player build in
`C:\Users\alstj\Documents\캡스톤 웜즈\Builds\EOS0924\` also launched,
created a public EOS lobby, left it, and exited cleanly. The 2-player quick
match path also created a waiting EOS lobby when no other room was available;
that room was closed and the game process exited. This is a local
development build, not a release package: it includes the development EOS
credentials in its StreamingAssets and still has the temporary executable
name `웜즈.exe`. Joining from a second device,
private-room admission/rejection, quick match under simultaneous load, P2P
gameplay and disconnect recovery remain unverified.

## One-time Epic setup on another machine

1. Sign in to the [Epic Developer Portal](https://dev.epicgames.com/portal/)
   and create/select a Product, Sandbox and Deployment.
2. Create a game client policy/client with a signed-in user requirement and the
   lobby `connect`, `readLobby`, and `findLobbies` actions. The current portal
   does not expose separate Connect or P2P toggles in this client policy.
3. In Unity open **EOS Plugin > EOS Configuration**. Enter Product ID, Client ID,
   Client Secret, Sandbox ID and Deployment ID for the same deployment; set a
   nonempty Product Version, then save. The plugin writes its local configuration
   to `Assets/StreamingAssets/EOS/`. The JSON files are git-ignored here.
4. Build two Windows clients from the same project/configuration. Test with two
   different devices/users. EOS Device ID login is bound to local device identity,
   so two builds on the same Windows account are not a reliable two-player test.

## Smoke test before release

- Public room: host creates, guest finds in room list and joins by six-character
  code. Verify selection, ready, turn actions, terrain changes and result on both.
- Private room: absent from public list and quick match; wrong password rejected;
  correct password joins by code. Never place the password in lobby attributes.
- Quick match: 2-player duel, 4-player free-for-all and 4-player 2v2; verify all
  player assignments and host-only combat resolution.
- Host disconnect during lobby and battle: guests return safely to title.
- Test across two networks (NAT/relay), plus unstable latency and large terrain
  state messages. EOS P2P packets are fragmented/reassembled by this project.

The editor host smoke test is **not** an end-to-end multiplayer test. Do not label
the online mode release-ready until the above matrix passes on two machines.
