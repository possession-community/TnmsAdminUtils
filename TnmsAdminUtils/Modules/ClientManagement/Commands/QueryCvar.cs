using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsExtendableTargeting.Shared;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.ClientManagement.Commands;

public class QueryCvar(IServiceProvider provider): TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "qcvar";
    public override string CommandDescription => "Queries client cvar value.";
    
    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.server.command.qcvar", true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new ExtendableTargetValidator(1, true, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "QueryCvar.Notification.Usage"));
                break;
            case ExtendableTargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        if (client == null)
            return;
        
        var targets = validatedArguments!.GetArgument<ITargetingResult>(1)!;

        var results = new QueryCvarResults(targets.GetTargets().Count(g => !g.IsFakeClient), SharedSystem);
        
        foreach (var target in targets.GetTargets())
        {
            if (target.IsFakeClient || target.IsHltv)
                continue;
            
            
            
            SharedSystem.GetClientManager().QueryConVar(target, commandInfo.GetArg(2),
                (targetCl, status, name, value) =>
                {
                    if (status != QueryConVarValueStatus.ValueIntact)
                    {
                        results.AddResult(targetCl, name, "<Failed>").PrintResultsToClient(client);
                        return;
                    }
                    results.AddResult(targetCl, name, value).PrintResultsToClient(client);
                });
        }
        
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {PlayerUtil.GetPlayerName(client)} requested query cvar '{commandInfo.GetArg(2)}' for {targets.GetTargetName()}");
        client.GetPlayerController()?.PrintToChat(((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.Notification.SeeConsole"));
    }

    private class QueryCvarResults(int targetClientCount, ISharedSystem sharedSystem): IDisposable
    {
        private List<string> ResultsToPrint { get; } = new();
        
        private const string FormatOutput = "{0} | {1} | {2}";
        
        public QueryCvarResults AddResult(IGameClient target, string cvarName, string cvarValue)
        {
            ResultsToPrint.Add(string.Format(FormatOutput, $"{target.Name,PrintPaddings.PlayerName}",
                $"{cvarName,PrintPaddings.CvarName}", $"{cvarValue,PrintPaddings.CvarValue}"));
            return this;
        }
        
        public void PrintResultsToClient(IGameClient client)
        {
            if (targetClientCount > ResultsToPrint.Count)
                return;
            
            sharedSystem.GetModSharp().InvokeFrameAction(() =>
            {
                client.ConsolePrint("================================ Cvar Query Results ================================\n");
                client.ConsolePrint($"{"Player Name", PrintPaddings.PlayerName} | {"Cvar Name", PrintPaddings.CvarName} | {"Cvar Value", PrintPaddings.CvarValue}\n");
                foreach (var result in ResultsToPrint)
                {
                    client.ConsolePrint(result);
                }
                Dispose();
            });
            
        }
        
        private class PrintPaddings
        {
            public const sbyte PlayerName = -32;
            public const sbyte CvarName = -48;
            public const sbyte CvarValue = -32;
        }

        public void Dispose()
        {
            ResultsToPrint.Clear();
        }
    }


}