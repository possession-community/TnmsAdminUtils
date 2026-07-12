using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.Chat.Commands;

public class PSay(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "psay";
    public override string CommandDescription => "Sends a private message to a player.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.chat.command.say", true))
        .Add(new ArgumentCountValidator(2, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "PSay.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case TargetValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NoValidTargetsFound"));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var targets = validatedArguments!.GetArgument<List<IGameClient>>(1)!;

        if (targets.Count != 1)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Common.ValidationFailure.MultipleTargetsFound"));
            return;
        }

        var target = targets[0];
        string message = string.Join(" ", commandInfo.ArgString.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1));
        string executor = PlayerUtil.GetPlayerName(client);
        string targetName = PlayerUtil.GetPlayerName(target);

        // Send to target
        target.GetPlayerController()?
            .PrintToChat(
                ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(target, "PSay.Broadcast.PrivateMessage", executor, targetName, message));

        // Send to executor
        if (client != null)
        {
            client.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "PSay.Broadcast.PrivateMessage", executor, targetName, message));
        }
    }
}
