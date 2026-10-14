/// <summary>Small input grace windows, evaluated only by the movement authority.</summary>
public sealed class ObjectHeadJumpWindow
{
    public const float CoyoteSeconds = .11f;
    public const float BufferSeconds = .14f;
    private float groundedUntil = float.NegativeInfinity;
    private float pressedUntil = float.NegativeInfinity;
    private bool waitingToLeaveGround;

    public bool Tick(float now, bool grounded, bool canMove, bool pressed)
    {
        if (!canMove) { Reset(); return false; }
        if (!grounded) waitingToLeaveGround = false;
        if (grounded && !waitingToLeaveGround) groundedUntil = now + CoyoteSeconds;
        if (pressed) pressedUntil = now + BufferSeconds;
        if (now > groundedUntil || now > pressedUntil) return false;
        Reset();
        waitingToLeaveGround = true;
        return true;
    }

    public void Reset()
    {
        groundedUntil = pressedUntil = float.NegativeInfinity;
        waitingToLeaveGround = false;
    }
}
