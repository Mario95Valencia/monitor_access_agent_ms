using DotNetEnv;

namespace monitor_access_agent_ms.libs
{
    public static class EnvironmentConfiguration
    {
        public static string GetConnectionString()
        {
            Env.Load(); // Carga el archivo .env en memoria

            var dbHost = Environment.GetEnvironmentVariable("DATABASE_HOST");
            var dbName = Environment.GetEnvironmentVariable("DATABASE_NAME");
            var dbUser = Environment.GetEnvironmentVariable("DATABASE_USER");
            var dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
            var dbSsl = Environment.GetEnvironmentVariable("DATABASE_SSL") ?? "true";

            return $"Server={dbHost};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt={dbSsl};TrustServerCertificate=true;";
        }

        public static string GetApiRootPath()
        {
            Env.Load();
            return Environment.GetEnvironmentVariable("API_ROOT_PATH") ?? "/api";
        }
    }
}
