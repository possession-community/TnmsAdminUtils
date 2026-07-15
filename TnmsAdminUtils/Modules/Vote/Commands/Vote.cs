using NativeVoteManagerMS.Shared;
using NativeVoteManagerMS.Shared.Types;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.Vote.Commands;

public class Vote(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "vote";
    public override string CommandDescription => "Starts a multi-choice vote.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.vote.command.vote", true))
        .Add(new ArgumentCountValidator(3, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Vote.Notification.Usage"));
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

        string question = commandInfo.GetArg(1);
        var voteContents = new List<VoteContent>();

        for (int i = 2; i < commandInfo.ArgCount; i++)
        {
            string optionText = commandInfo.GetArg(i);
            int index = i - 2;
            voteContents.Add(new VoteContent
            {
                Index = index,
                InternalName = $"option_{index}",
                VisibleName = LocalizedString.From(_ => optionText),
            });
        }

        var options = new MultiChoiceVoteOptions
        {
            Title = LocalizedString.From(_ => question),
            Description = LocalizedString.From(_ => ""),
            Participants = null,
            PassCondition = MajorityCondition,
            VoteDuration = 15.0f,
            VoteHandler = new VoteHandler(SharedSystem),
            VoteContents = voteContents
        };

        voteManager.InitiateMultiChoiceVote(options);

        string executor = PlayerUtil.GetPlayerName(client);
        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "Vote.Broadcast.VoteStarted", executor, question));
        }
    }

    private static bool MajorityCondition(VoteResult result)
    {
        if (result.Winner != null)
            return true;

        var sorted = result.Choices.OrderByDescending(c => c.Voters.Count).ToList();
        return sorted.Count > 0 && sorted[0].Voters.Count > 0;
    }

    private class VoteHandler(Sharp.Shared.ISharedSystem sharedSystem) : IMultiChoiceVoteHandler
    {
        public void OnVotePassed(VoteResult result)
        {
            if (result.Winner == null)
                return;

            string winnerName = result.Winner.VisibleName.Resolve();
            int count = result.Choices.First(c => c.Content.InternalName == result.Winner.InternalName).Voters.Count;

            sharedSystem.GetModSharp().PrintToChatAll($"Vote result: {winnerName} ({count} votes)");
        }

        public void OnVoteFailed(VoteResult result)
        {
            sharedSystem.GetModSharp().PrintToChatAll("Vote failed.");
        }
    }
}
