using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using TnmsPluginFoundation;
using TnmsPluginFoundation.Extensions.Client;

namespace TnmsAdminUtils.Modules.Chat;

public class AdminChatListener(TnmsAdminUtils plugin, ISharedSystem sharedSystem) : IClientListener
{
    public int ListenerVersion => 1;
    public int ListenerPriority => 0;

    public ECommandAction OnClientSayCommand(IGameClient client, bool teamOnly, bool isCommand, string commandName, string message)
    {
        if (!teamOnly)
            return ECommandAction.Skipped;

        if (!message.StartsWith("@ ") || message.Length <= 2)
            return ECommandAction.Skipped;

        if (!TnmsPlugin.AdminManager.PlayerHasPermission(client.SteamId, "tnms.adminutil.chat.command.say"))
            return ECommandAction.Skipped;

        string body = message[2..].Trim();
        if (string.IsNullOrWhiteSpace(body))
            return ECommandAction.Skipped;

        string senderName = client.Name;

        foreach (var gameClient in sharedSystem.GetModSharp().GetIServer().GetGameClients())
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            bool isAdmin = TnmsPlugin.AdminManager.PlayerHasPermission(gameClient.SteamId, "tnms.adminutil.chat.command.say");

            string formatted = isAdmin
                ? plugin.LocalizeWithPluginPrefix(gameClient, "AdminChat.Broadcast.ToAdmin", senderName, body)
                : plugin.LocalizeWithPluginPrefix(gameClient, "AdminChat.Broadcast.ToAll", body);

            gameClient.GetPlayerController()?.PrintToChat(formatted);
        }

        return ECommandAction.Stopped;
    }
}
