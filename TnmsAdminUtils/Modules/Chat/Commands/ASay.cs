using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.Chat.Commands;

public class ASay(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "asay";
    public override string CommandDescription => "Sends a message to admins only.";

    private const string Permission = "tnms.adminutil.chat.command.say.admins";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).InCategory(AdminMenuCategory.Notification).Usage("ASay.Notification.Usage").Text("AdminMenu.Step.Message"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "ASay.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        string message = commandInfo.ArgString;
        string executor = PlayerUtil.GetPlayerName(client);
        var adminManager = TnmsPluginFoundation.TnmsPlugin.AdminManager;

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            if (!adminManager.PlayerHasPermission(gameClient.SteamId, "tnms.adminutil.chat.command.say.admins"))
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    Plugin.LocalizeStringForPlayer(gameClient, "Say.Broadcast.ASay", executor, message));
        }
    }
}
