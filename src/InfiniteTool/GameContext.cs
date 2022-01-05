using InfiniteTool.GameInterop;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace InfiniteTool
{

    public class GameContext : INotifyPropertyChanged
    {
        private readonly ILogger<GameContext> logger;

        public event PropertyChangedEventHandler? PropertyChanged;

        public GameInstance Instance { get; private set; }
        public GamePersistence Persistence { get; private set; }

        public GameContext(GameInstance instance, GamePersistence progression, ILogger<GameContext> logger)
        {
            this.Instance = instance;
            this.Persistence = progression;
            this.logger = logger;
        }        
    }
}
