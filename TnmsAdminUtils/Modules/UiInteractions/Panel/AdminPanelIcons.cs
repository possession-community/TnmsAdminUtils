namespace TnmsAdminUtils.Modules.UiInteractions.Panel;

/// <summary>
/// Icons of the panel's sidebar. An icon is the name of one of CS2's own UI icons
/// (s2r://panorama/images/icons/ui/&lt;name&gt;.vsvg) listed in <see cref="Names"/>, drawn as the background of the icon
/// panel by the class ico-&lt;name&gt; (tnms_admin_panel.vcss has one rule per name: keep both lists the same), or any
/// other text, drawn as a glyph.
/// </summary>
public static class AdminPanelIcons
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        "addplayer", "alert", "arrowhead", "bomb_c4", "bot", "broadcast_ring", "buyzone", "camera",
        "cancel", "casual", "check", "clock", "community_servers", "competitive", "crosshair_circle", "ct_logo_1c",
        "defuser_white", "elimination", "exit", "favorite_star_filled", "film", "filter", "filter_team", "find",
        "gift", "graph", "home", "hostage_alive", "hourglass", "info", "info_i", "inventory",
        "invite", "kill", "kill_headshot", "leader", "link", "lobby", "locked", "map_onmap",
        "menu", "message_arrow", "music_kit", "muted", "overwatch", "picture", "player", "plus",
        "power", "random", "refresh", "remove", "report_server", "search", "secure_connection", "settings",
        "settings_sliders", "shield", "shield_alert", "smile", "sort", "sound_2", "sound_off", "star",
        "stats", "stream", "t_logo_1c", "teamcolor", "timer", "trade", "trash", "trophy",
        "tune", "undo", "unmuted", "vacnet", "vote_check", "votesurrender", "voteteamswitch", "warning",
        "watch", "watch_tv", "zoom_in",
    };

    // The sidebar's Previous / Next: arrowhead turned up / down (tap-ico-up / -down).
    public const string Up = "\0up";
    public const string Down = "\0down";

    public static bool IsImage(string icon) => Names.Contains(icon) || icon is Up or Down;
}
