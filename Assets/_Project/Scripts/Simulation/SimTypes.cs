namespace MarbleSync.Simulation
{
    /// <summary>Kinds of point elements placed along a track.</summary>
    public enum ElementKind
    {
        Curve,
        Honey,
        Oil,
        Water,
        Flame,
        Button,
        Door,
        Sensor
    }

    /// <summary>Surface coating carried by a marble (one at a time).</summary>
    public enum Coating
    {
        None,
        Honey,
        Oil,
        Wet
    }

    /// <summary>Special condition layered on top of the coating.</summary>
    public enum Condition
    {
        Normal,
        Caramelized,
        Burning
    }

    public enum MarbleStatus
    {
        Waiting,
        Rolling,
        Caramelized,
        StoppedAtDoor,
        OnSeesaw,
        Arrived,
        Melted,
        Blocked
    }

    /// <summary>Colour channel shared by buttons, doors and flames.</summary>
    public enum LogicColor
    {
        Blue,
        Red,
        Green,
        Yellow
    }

    public enum FailureReason
    {
        None,
        BlockedBySeesaw,
        Melted,
        NeverArrived
    }

    public enum TimerTrigger
    {
        AttemptStart,
        MarbleReleased,
        SensorPassed,
        ButtonPressed
    }

    public enum SimEventType
    {
        AttemptStarted,
        Released,
        Resumed,
        CoatingChanged,
        CurvePassed,
        Caramelized,
        Ignited,
        Extinguished,
        Evaporated,
        Melted,
        ButtonPressed,
        LogicToggled,
        SensorPassed,
        StoppedAtDoor,
        LeftDoor,
        EnteredSeesaw,
        SeesawClosed,
        Blocked,
        Arrived,
        MeltedAtFinish,
        AttemptFinished
    }

    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly int Marble;
        public readonly int Element;
        public readonly int Step;

        public SimEvent(SimEventType type, int marble, int element, int step)
        {
            Type = type;
            Marble = marble;
            Element = element;
            Step = step;
        }
    }
}
