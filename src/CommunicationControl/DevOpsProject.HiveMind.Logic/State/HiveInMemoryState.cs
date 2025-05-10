using DevOpsProject.Shared.Models;

namespace DevOpsProject.HiveMind.Logic.State
{
    public static class HiveInMemoryState
    {
        private static readonly object _operationalAreaLock = new();
        private static readonly object _telemetryLock = new();
        private static readonly object _movementLock = new();

        private static HiveOperationalArea _operationalArea;

        private static bool _isTelemetryRunning;
        private static bool _isMoving;

        private static Location? _currentLocation;
        private static Location? _destination;


        public static HiveOperationalArea OperationalArea
        {
            get
            {
                lock (_operationalAreaLock)
                {
                    return _operationalArea;
                }
            }
            set
            {
                lock (_operationalAreaLock)
                {
                    _operationalArea = value;
                }
            }
        }

        public static bool IsTelemetryRunning
        {
            get
            {
                lock (_telemetryLock)
                {
                    return _isTelemetryRunning;
                }
            }
            set
            {
                lock (_telemetryLock)
                {
                    _isTelemetryRunning = value;
                }
            }
        }

        public static bool IsMoving
        {
            get 
            { 
                lock (_movementLock) 
                { 
                    return _isMoving; 
                } 
            }
            set 
            { 
                lock (_movementLock) 
                { 
                    _isMoving = value; 
                } 
            }
        }

        public static Location? CurrentLocation
        {
            get
            {
                lock (_movementLock) { return _currentLocation; }
            }
            set
            {
                lock (_movementLock) { _currentLocation = value; }
            }
        }

        public static Location? Destination
        {
            get
            {
                lock (_movementLock) { return _destination; }
            }
            set
            {
                lock (_movementLock) { _destination = value; }
            }
        }
    }
}
