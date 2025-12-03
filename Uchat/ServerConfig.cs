namespace Uchat
{
    /// <summary>
    /// Централизованная конфигурация URL сервера
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// ЛОКАЛЬНЫЙ DOCKER СЕРВЕР
        /// Используется для локальной разработки и тестирования
        /// 
        /// Для менторов:
        /// 1. Запустите Docker: docker-compose up
        /// 2. Запустите клиент: cd Uchat && dotnet run
        /// 3. Сервер доступен на http://localhost:5000
        /// </summary>
        public const string LocalDockerUrl = "http://localhost:5000";
        
        /// <summary>
        /// URL для Railway продакшена (будет настроен позже)
        /// </summary>
        public const string ProductionUrl = "https://your-app.railway.app";
        
        /// <summary>
        /// Текущий активный URL сервера
        /// В DEBUG режиме использует Docker, в Release - Railway
        /// </summary>
        public static string ApiBaseUrl => 
#if DEBUG
            LocalDockerUrl;
#else
            ProductionUrl;
#endif
        
        /// <summary>
        /// URL для SignalR Hub
        /// </summary>
        public static string ChatHubUrl => $"{ApiBaseUrl}/chatHub";
    }
}
