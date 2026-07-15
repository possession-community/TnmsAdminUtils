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
        if (!message.StartsWith('@') || message.Length <= 1)
            return ECommandAction.Skipped;

        if (!TnmsPlugin.AdminManager.PlayerHasPermission(client.SteamId, "tnms.adminutil.chat.command.say.admins"))
            return ECommandAction.Skipped;

        string body = message[1..].Trim();
        if (string.IsNullOrWhiteSpace(body))
            return ECommandAction.Skipped;

        string senderName = client.Name;
        string adminMsg = plugin.LocalizeWithPluginPrefix(null, "AdminChat.Broadcast.ToAdmin", senderName, body);
        string publicMsg = plugin.LocalizeWithPluginPrefix(null, "AdminChat.Broadcast.ToAll", body);

        foreach (var gameClient in sharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            bool isAdmin = TnmsPlugin.AdminManager.PlayerHasPermission(gameClient.SteamId, "tnms.adminutil.chat.command.say.admins");
            gameClient.GetPlayerController()?.PrintToChat(isAdmin ? adminMsg : publicMsg);
        }

        return ECommandAction.Stopped;
    }
}
