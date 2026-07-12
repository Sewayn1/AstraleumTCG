namespace Astraleum.AI
{
    /// <summary>Ne joue jamais — sert uniquement à tester ses propres decks sans adversité.
    /// Son tour doit tout de même se terminer (voir LocalAIGameController.RunAITurnCoroutine).</summary>
    public class SandboxAIController : IAIController
    {
        public AIAction DecideNextAction(int aiPlayerID) => null;
    }
}
