namespace Uchat
{
    /// <summary>
    /// Централизованная конфигурация URL сервера
    /// Измените NgrokUrl после запуска ngrok
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// URL ngrok для удаленного доступа
        /// ВАЖНО: Обновите этот URL после запуска ngrok!
        /// 
        /// Шаги:
        /// 1. Запустите сервер: cd Uchat.Server && dotnet run
        /// 2. В другом терминале: ngrok http 5180
        /// 3. Скопируйте URL из ngrok (например: https://abc123.ngrok-free.app)
        /// 4. Вставьте сюда вместо значения ниже
        /// </summary>
        public const string NgrokUrl = "https://unghostly-bunglingly-elli.ngrok-free.dev";
        
        /// <summary>
        /// Локальный URL для разработки
        /// </summary>
        public const string LocalUrl = "http://localhost:5180";
        
        /// <summary>
        /// Использовать ngrok (true) или локальный сервер (false)
        /// </summary>
        public const bool UseNgrok = true;
        
        /// <summary>
        /// Текущий активный URL сервера
        /// </summary>
        public static string ServerUrl => UseNgrok ? NgrokUrl : LocalUrl;
        
        /// <summary>
        /// URL для SignalR Hub
        /// </summary>
        public static string ChatHubUrl => $"{ServerUrl}/chatHub";
    }
}
