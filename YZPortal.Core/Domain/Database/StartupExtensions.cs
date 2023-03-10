using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YZPortal.Core.Domain.Contexts;
using static System.Formats.Asn1.AsnWriter;

namespace YZPortal.Core.Domain.Database
{
    public static class StartupExtensions
    {
        public static void AddDatabaseService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
            services.AddTransient<DatabaseService>();
        }

        public static void UseDatabaseService(this WebApplication app)
        {
			using (var scope = app.Services.CreateScope())
			{
				var services = scope.ServiceProvider;

				// Make sure the latest EFCore migration is applied everytime the API is initiated
				var dbContext = services.GetRequiredService<PortalContext>();
				dbContext.Database.Migrate();

				// Applied before migration so that DatabaseService transaction can take place for new/existing DB.
				// Make sure to arrange the database service sequentially as chronology affects the functionality
				var service = services.GetRequiredService<DatabaseService>();
				service.UserAdmin();
				service.EnumValues();
				service.SyncStatuses();
			}
        }
    }
}
