namespace Astraleum.AI
{
    /// <summary>Fabrique le contrôleur IA correspondant au mode de partie. Null en Sandbox (ne joue jamais).</summary>
    public static class AIControllerFactory
    {
        public static IAIController Create(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.AISandbox:
                    return new SandboxAIController();
                case GameMode.AIEasy:
                    return new EasyAIController();
                case GameMode.AIMedium:
                    return new MediumAIController();
                case GameMode.AIHard:
                    return new HardAIController();
                default:
                    return null;
            }
        }
    }
}
