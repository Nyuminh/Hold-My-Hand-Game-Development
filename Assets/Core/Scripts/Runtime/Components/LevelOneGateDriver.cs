namespace Blocks.Gameplay.Core
{
    // Keeps Core independent of the CastlePack's Assembly-CSharp assembly.
    public interface ILevelOneGateDriver
    {
        void BeginClimaxControl();
        void SetClimaxLift(float metres);
    }
}
