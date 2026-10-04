namespace SealGugu
{
    /// <summary>Gameplay balance independent of chart density and timing calibration.</summary>
    public sealed class DifficultyBalance
    {
        public double oxygen, fullRatio, missPenalty, perfectRecovery, goodRecovery;
        public static DifficultyBalance For(string level)
        {
            if(level=="beginner")return new DifficultyBalance{oxygen=.5,fullRatio=.60,missPenalty=6,perfectRecovery=3,goodRecovery=1.5};
            if(level=="intermediate")return new DifficultyBalance{oxygen=.5,fullRatio=.70,missPenalty=8,perfectRecovery=2.2,goodRecovery=1};
            return new DifficultyBalance{oxygen=1,fullRatio=360.0/438,missPenalty=11,perfectRecovery=1.5,goodRecovery=.5};
        }
    }
}
