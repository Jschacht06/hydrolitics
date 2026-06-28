using Hydrolitics.Api.Configuration;
using Hydrolitics.Api.Services;

DotNetEnv.Env.TraversePath().Load(); 
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


//MQTT
var mqttOptions = builder.Configuration.GetSection("Mqtt").Get<MqttOptions>() ?? new MqttOptions();
builder.Services.AddSingleton(mqttOptions);
builder.Services.AddHostedService<MqttService>();

//Influx
var influxOptions = builder.Configuration.GetSection("Influx").Get<InfluxOptions>() ?? new InfluxOptions();
builder.Services.AddSingleton(influxOptions);
builder.Services.AddSingleton<InfluxWriter>();
builder.Services.AddSingleton<InfluxReader>();




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
