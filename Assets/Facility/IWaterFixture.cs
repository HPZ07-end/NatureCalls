namespace NatureCalls.Facility
{
    /// <summary>A fixture connected to at most one Pipe.</summary>
    public interface IWaterFixture
    {
        Pipe ConnectedPipe { get; }
        void Backflow();
    }
}
