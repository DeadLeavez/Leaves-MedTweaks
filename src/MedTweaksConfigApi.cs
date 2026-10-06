using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers.Http;

namespace Leaves_MedTweaks;

// The web config surface. Registering these two routes with Kind "config" is what makes the
// shared /webui/config editor pick this mod up (GET = view, GET + POST = editable).
//
//   GET  /medtweaks/config  -> { Schema, Current }   (HttpUtils.WriteConfigView)
//   POST /medtweaks/config  -> { Ok, Messages }      (mod.ApplyConfigJson -> HttpUtils.WriteConfigApply)
[Injectable]
public class MedTweaksConfigApi(MedTweaks mod) : IHttpListener, IOnLoad
{
    private const string ConfigPath = "/medtweaks/config";

    public Task OnLoad()
    {
        var routes = new List<Leaves.RouteInfo>
        {
            new Leaves.RouteInfo("GET", ConfigPath, "Config: current values + schema.", "config"),
            new Leaves.RouteInfo("POST", ConfigPath, "Config: apply edits.", "config", danger: true),
        };
        Leaves.Utils.Instance.RegisterRoutes("Med Tweaks", routes);
        return Task.CompletedTask;
    }

    public bool CanHandle(MongoId sessionId, HttpContext context)
    {
        return Leaves.HttpUtils.HasPrefix(context, "/medtweaks/");
    }

    public async Task Handle(MongoId sessionId, HttpContext context)
    {
        if (Leaves.HttpUtils.IsGet(context, ConfigPath))
        {
            await Leaves.HttpUtils.WriteConfigView<MedTweaksConfig>(context, mod.CurrentConfig);
            return;
        }

        if (Leaves.HttpUtils.IsPost(context, ConfigPath))
        {
            string body = await Leaves.HttpUtils.ReadBody(context);
            Leaves.HttpUtils.ConfigApplyResult result = mod.ApplyConfigJson(body);
            await Leaves.HttpUtils.WriteConfigApply(context, result);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync($"Unknown Med Tweaks resource: {context.Request.Path}");
    }
}
