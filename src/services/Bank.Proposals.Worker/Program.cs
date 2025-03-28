using Bank.Proposals.Configurations;
using Bank.Proposals.Worker.Configurations;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();
services.AddDependecyResolver();
services.AddLogConfig();
services.AddMessageConfig();

var app = builder.Build();



app.Run();


