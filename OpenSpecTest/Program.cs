using OpenSpec.API.Tasks;
using OpenSpec.Application.Services;
using OpenSpec.Infraestructure.Repository;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Inyección de dependencias para los repositorios y servicios del motor
string connectionString = builder.Configuration.GetConnectionString("SqlAudit")
    ?? "Server=localhost,1433;Database=AnomalyTestDb;User Id=sa;Password=SecurePassword123!;TrustServerCertificate=True;";

builder.Services.AddSingleton(new SqlAuditRepository(connectionString));
builder.Services.AddSingleton(new HybridDetectionEngine());

builder.Services.AddHostedService<AuditMonitoringWorker>(); 
builder.Services.AddSingleton(new AuditTrafficGenerator(connectionString));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseAuthorization();

app.MapControllers();

app.Run();
