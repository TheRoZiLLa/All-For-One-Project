namespace Recorded.Enemies
{
    public interface IEnemyBrain
    {
        bool IsDead { get; }
        void PauseForColor();
        void ResumeAfterColor();
        void InterruptForRed();
        void Die();
        void Revive();
    }
}
