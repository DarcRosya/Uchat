namespace Uchat
{
    /// <summary>
    /// Централизованная конфигурация URL сервера
    /// Измените NgrokUrl после запуска ngrok
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// ЦЕНТРАЛЬНЫЙ СЕРВЕР - используется всеми клиентами
        /// Этот URL должен указывать на единственный работающий сервер
        /// 
        /// ВАЖНО для разработчиков:
        /// - НЕ запускайте локальный сервер (Uchat.Server)
        /// - Используйте только клиент (Uchat)
        /// - Все подключаются к этому центральному серверу
        /// </summary>
        public const string CentralServerUrl = "https://unghostly-bunglingly-elli.ngrok-free.dev";
        
        /// <summary>
        /// Локальный URL - ТОЛЬКО для тестирования сервера
        /// Обычные пользователи НЕ должны это использовать
        /// </summary>
        public const string LocalUrl = "http://localhost:5180";
        
        /// <summary>
        /// Режим разработки сервера (false = все клиенты на центральный сервер)
        /// Установите true ТОЛЬКО если вы разработчик сервера и тестируете изменения
        /// </summary>
        public const bool UseLocalServer = false;
        
        /// <summary>
        /// Текущий активный URL сервера
        /// </summary>
        public static string ServerUrl => UseLocalServer ? LocalUrl : CentralServerUrl;
        
        /// <summary>
        /// URL для SignalR Hub
        /// </summary>
        public static string ChatHubUrl => $"{ServerUrl}/chatHub";
    }
}
