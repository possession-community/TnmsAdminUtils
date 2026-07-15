using NativeVoteManagerMS.Shared;
using NativeVoteManagerMS.Shared.Types;
using Sharp.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.Vote.Commands;

public class YesNoVote(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "ynvote";
    public override string CommandDescription => "Starts a Yes/No vote.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.vote.command.vote", true))
        .Add(new ArgumentCountValidator(1, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "YesNoVote.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        var voteManager = SharedSystem.GetSharpModuleManager()
            .GetRequiredSharpModuleInterface<INativeVoteManager>(INativeVoteManager.ModSharpModuleIdentity)
            .Instance!;

        if (voteManager.IsAnyVoteInProgress)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Vote.Notification.InProgress"));
            return;
        }

        string question = commandInfo.ArgString;
        int initiatorSlot = client?.Slot ?? 99;

        var options = new YesNoVoteOptions
        {
            Title = "#SFUI_Vote_None",
            Description = LocalizedString.From(_ => question),
            Participants = null,
            PassCondition = MajorityCondition,
            VoteDuration = 15.0f,
            VoteHandler = new YnVoteHandler(SharedSystem, (TnmsAdminUtils)Plugin, question),
            VoteInitiator = initiatorSlot,
        };

        voteManager.InitiateYesNoVote(options);

        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} started a yes/no vote: {question}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "YesNoVote.Broadcast.VoteStarted", executor, question));
        }
    }

    private static bool MajorityCondition(VoteResult result)
    {
        int yes = result.Choices[0].Voters.Count;
        int no = result.Choices[1].Voters.Count;
        return yes > no;
    }

    private class YnVoteHandler(ISharedSystem sharedSystem, TnmsAdminUtils plugin, string question) : IYesNoVoteHandler
    {
        public void OnVotePassed(VoteResult result)
        {
            int yes = result.Choices[0].Voters.Count;
            int no = result.Choices[1].Voters.Count;

            foreach (var gameClient in sharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;

                gameClient.GetPlayerController()?
                    .PrintToChat(
                        plugin.LocalizeWithPluginPrefix(gameClient, "YesNoVote.Broadcast.VotePassed", question, yes, no));
            }
        }

        public void OnVoteFailed(VoteResult result)
        {
            int yes = result.Choices[0].Voters.Count;
            int no = result.Choices[1].Voters.Count;

            foreach (var gameClient in sharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
            {
                if (gameClient.IsFakeClient || gameClient.IsHltv)
                    continue;

                gameClient.GetPlayerController()?
                    .PrintToChat(
                        plugin.LocalizeWithPluginPrefix(gameClient, "YesNoVote.Broadcast.VoteFailed", question, yes, no));
            }
        }
    }
}
