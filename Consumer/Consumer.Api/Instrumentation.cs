using System.Diagnostics;

namespace Consumer.Api
{
    /// <summary>
    /// It is recommended to use a custom type to hold references for ActivitySource.
    /// This avoids possible type collisions with other components in the DI container.
    /// </summary>
    public class Instrumentation : IDisposable
    {
        public const string ServiceName = "consumer";
        public const string ServiceVersion = "1.0.0";

        public Instrumentation()
        {
            this.ActivitySource = new ActivitySource(ServiceName, ServiceVersion);
        }

        public ActivitySource ActivitySource { get; }

        public void Dispose()
        {
            this.ActivitySource.Dispose();
        }
    }
}
