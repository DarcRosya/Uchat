namespace LoginFormAvalonia
{
    /// <summary>
    /// Централизованная конфигурация URL сервера для формы входа
    /// Используйте ту же конфигурацию что и в Uchat.ServerConfig
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// URL ngrok для удаленного доступа
        /// ВАЖНО: Должен совпадать с Uchat.ServerConfig!
        /// </summary>
        public const string NgrokUrl = "https://unghostly-bunglingly-elli.ngrok-free.app";
        
        /// <summary>
        /// Локальный URL для разработки
        /// </summary>
        public const string LocalUrl = "http://localhost:5180";
        
        /// <summary>
        /// Использовать ngrok (true) или локальный сервер (false)
        /// </summary>
        public const bool UseNgrok = false;
        
        /// <summary>
        /// Текущий активный URL сервера
        /// </summary>
        public static string ServerUrl => UseNgrok ? NgrokUrl : LocalUrl;
    }
}
