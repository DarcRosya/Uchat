namespace Uchat
{
    /// <summary>
    /// Централизованная конфигурация URL сервера
    /// Поддерживает два режима:
    /// 1. Локальный (UseNgrok = false) - для разработки одного человека
    /// 2. Сетевой (UseNgrok = true) - для работы через ngrok с внешним доступом
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// РЕЖИМ РАБОТЫ
        /// false = Локальный сервер (http://localhost:5000)
        /// true = Ngrok туннель (внешний доступ)
        /// </summary>
        public const bool UseNgrok = false;
        
        /// <summary>
        /// URL ngrok туннеля
        /// ВАЖНО: Обновляйте после каждого запуска ngrok!
        /// 
        /// Как получить:
        /// 1. Запустите сервер: cd Uchat.Server && dotnet run
        /// 2. В другом терминале: ngrok http 5000
        /// 3. Скопируйте URL из ngrok (https://xxxx.ngrok-free.app)
        /// 4. Вставьте сюда
        /// </summary>
        public const string NgrokUrl = "https://your-ngrok-url.ngrok-free.app";
        
        /// <summary>
        /// Локальный URL сервера
        /// Используется когда UseNgrok = false
        /// </summary>
        public const string LocalUrl = "http://localhost:5000";
        
        /// <summary>
        /// Активный URL API
        /// Автоматически переключается между локальным и ngrok
        /// </summary>
        public static string ApiBaseUrl => UseNgrok ? NgrokUrl : LocalUrl;
        
        /// <summary>
        /// URL для SignalR Hub
        /// </summary>
        public static string ChatHubUrl => $"{ApiBaseUrl}/chatHub";
    }
}