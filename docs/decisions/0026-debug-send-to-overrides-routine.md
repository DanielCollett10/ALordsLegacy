# 0026: Debug send-to overrides the routine through state on the citizen

**Context.** PPL-33 wants a debug action that walks one citizen to a clicked point, lingers 30 game minutes, then
returns it to its routine. The routine (T-259) runs before movement and would otherwise hand the citizen new orders
while it walks or lingers.

**Decision.**
- The send is a command, `SendCitizenTo` (decision 0011), that stores a `DebugSendState` on the `Citizen` (the walk
  order and the linger end tick). Validation rejects an unknown citizen (`NotFound`) and a point with no route from
  where the citizen stands (`NotAllowed`, "No route").
- While `Citizen.DebugSend` is set, `RoutineSystem` skips the citizen and forgets its routine state. A new
  `DebugSendSystem`, in the movement slot after `MovementSystem`, handles arrival, the linger and the end.
- When the send ends, the routine starts afresh. It does not resume the state it had before the send.
- The linger length is `SimTuning.DebugLingerMinutes` (30, tuning.md).

**Consequences.** Overrides are visible in the Sim state, so the UI can show them. Nothing is queued, so the send
works while paused and takes effect on the next tick. Other future overrides (work, dispatch) can reuse the same
pattern by checking their own state in the routine. A debug-only build gate (PPL-33 "not in player builds") is not
in the Sim; the Unity layer must hide the command.

Update (T-264): `DebugSendController` (Unity) does this. It is active only in the Editor and development builds.
