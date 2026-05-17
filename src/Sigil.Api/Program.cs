using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Infrastructure.Gateway;
using Sigil.Infrastructure.Security;
using Sigil.Runtime.DependencyInjection;
using Sigil.Storage.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSigilSecurity(builder.Configuration);
builder.Services.AddSigilEfCore(builder.Configuration);
builder.Services.AddAgentGateway(builder.Configuration);
builder.Services.AddSigilRuntime();
builder.Services.AddFastEndpoints(o => o.Assemblies = [typeof(Program).Assembly]);

var app = builder.Build();
app.UseMiddleware<SigilAuthMiddleware>();
app.UseFastEndpoints();
app.Run();

public partial class Program;
