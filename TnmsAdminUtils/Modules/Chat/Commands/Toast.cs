using System.Globalization;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsAdminUtils.Modules.UiInteractions;
using TnmsAdminUtils.Utils;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using Wuling.Abstract.Tianshi.Liuli;

namespace TnmsAdminUtils.Modules.Chat.Commands;

public class Toast(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "toast";
    public override string CommandDescription => "Shows a toast notification to players.";

    private const string Permission = "tnms.adminutil.chat.command.say.toast";

    private const float MaxSeconds = 60f;

    private static readonly Dictionary<string, LiuliToastKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["info"] = LiuliToastKind.Info,
        ["success"] = LiuliToastKind.Success,
        ["warning"] = LiuliToastKind.Warning,
        ["error"] = LiuliToastKind.Error,
    };

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).InCategory(AdminMenuCategory.Notification).Usage("Toast.Notification.Usage")
            .Target()
            .Choice("AdminMenu.Step.ToastType",
                new AdminMenuChoice("info", "AdminMenu.Choice.Toast.Info"),
                new AdminMenuChoice("success", "AdminMenu.Choice.Toast.Success"),
                new AdminMenuChoice("warning", "AdminMenu.Choice.Toast.Warning"),
                new AdminMenuChoice("error", "AdminMenu.Choice.Toast.Error"))
            .Text("AdminMenu.Step.Message")
            .Preset("toast", "AdminMenu.Step.ToastTime").Optional());

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(3, true))
        .Add(new TargetValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Toast.Notification.Usage"));
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

        if (!Kinds.TryGetValue(commandInfo.GetArg(2), out var kind))
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Toast.Notification.InvalidType"));
            return;
        }

        var words = commandInfo.ArgString.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToList();

        // A trailing "<seconds>s" is the display time; a plain number stays part of the message.
        TimeSpan? duration = null;

        if (words.Count > 1 && TryParseSeconds(words[^1], out var seconds))
        {
            duration = TimeSpan.FromSeconds(Math.Min(seconds, MaxSeconds));
            words.RemoveAt(words.Count - 1);
        }

        var message = string.Join(" ", words);

        if (message.Length == 0)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Toast.Notification.Usage"));
            return;
        }

        var executor = PlayerUtil.GetPlayerName(client);
        var toast = TnmsPlugin.Wuling.Liuli.Toast;

        foreach (var target in targets)
        {
            if (target.IsFakeClient || TnmsPlugin.Wuling.Registry.GetPlayer(target) is not { } player)
                continue;

            toast.Show(player, kind, Plugin.LocalizeStringForPlayer(target, "Toast.Title", executor), message, duration);
        }

        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} sent a {kind} toast to {targets.GetTargetName()}: {message}");
        PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Toast.Notification.Sent", targets.GetTargetName()));
    }

    private static bool TryParseSeconds(string word, out float seconds)
    {
        seconds = 0;

        return word.Length > 1
               && word.EndsWith('s')
               && float.TryParse(word.AsSpan(0, word.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
               && seconds > 0;
    }
}
