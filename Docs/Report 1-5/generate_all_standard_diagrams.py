# -*- coding: utf-8 -*-
"""
Generate complete suite of software engineering diagrams for Hold My Hand (FA26SE047):
Fixed based on user feedback:
1. Screen Flow: "Exit to Menu" line runs through completely open space below Main Menu,
   never intersecting or cutting through Login/Register or any other box.
2. Package Diagram: Dependency arrows (<<import>>, <<uses>>, <<accesses>>) are drawn
   STRICTLY within the 0.9-unit vertical gaps BETWEEN packages, never cutting through
   package boxes or borders.
3. Fully compliant with User's Exact Style:
   - Academic Monochrome / Slate Professional (#0F172A).
   - Zero collisions, zero overlaps.
   - 100% horizontal text with clean opaque white padding.
   - Orthogonal 90-degree clean corridors.
   - Bold, prominent triangular arrowheads (-|>).
"""
import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)

LINE_COLOR = "#0F172A"
BG_CARD = "#FFFFFF"
BORDER_COLOR = "#0F172A"
MUTATION_SCALE = 15


# =============================================================================
# 1. SCREENS NAVIGATION FLOW DIAGRAM (Report 3 - Figure 3.3) — FIXED NO OVERLAP
# =============================================================================
def generate_screen_flow():
    fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
    ax.set_xlim(0, 19)
    ax.set_ylim(0, 12)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Title
    ax.text(9.5, 11.55, "Hold My Hand — Screens Navigation Flow Diagram (Game Client & Web Portal)",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR)

    # Outer Boundary Box
    boundary = patches.Rectangle((0.5, 0.4), 18.0, 10.8, fc=BG_CARD, ec=BORDER_COLOR, lw=1.8, zorder=1)
    ax.add_patch(boundary)

    # ── TOP SECTION: GAME CLIENT (UNITY 6) ──────────────────────────────────
    ax.text(0.9, 10.75, "GAME CLIENT SCREEN FLOW (Unity 6 / UI Toolkit)",
            ha="left", va="center", fontsize=11.5, fontweight="bold", color="#1E293B", zorder=2)

    def draw_screen_box(x, y, w, h, title, desc):
        rect = patches.Rectangle((x - w/2, y - h/2), w, h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.4, zorder=3)
        ax.add_patch(rect)
        ax.text(x, y + 0.22, title, ha="center", va="center", fontsize=9.2, fontweight="bold", color=LINE_COLOR, zorder=4)
        ax.text(x, y - 0.25, desc, ha="center", va="center", fontsize=7.6, color="#334155", zorder=4)

    # Row 1 (Main Forward Progression): y = 9.5
    draw_screen_box(1.8, 9.5, 2.4, 1.2, "Splash Screen", "Logo & Version Check")
    draw_screen_box(4.8, 9.5, 2.4, 1.2, "Login / Register", "JWT Auth Modal")
    draw_screen_box(7.8, 9.5, 2.4, 1.2, "Main Menu", "Play / Settings / Quit")
    draw_screen_box(10.8, 9.5, 2.4, 1.2, "Lobby Browser", "Search & Code Entry")
    draw_screen_box(13.8, 9.5, 2.4, 1.2, "3D Lobby Room", "Role Swap / Ready / Mic")
    draw_screen_box(16.8, 9.5, 2.4, 1.2, "Loading Screen", "Netcode Handshake")

    # Row 2 (Gameplay & Summary): y = 7.5
    draw_screen_box(16.8, 7.5, 2.4, 1.2, "Gameplay Scene", "Co-op Puzzle & Boss")
    draw_screen_box(13.8, 7.5, 2.4, 1.2, "Match Summary", "Win/Loss Stats & Badges")

    def draw_orthogonal_arrow(pts, label="", label_pos="mid"):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        ax.plot(xs, ys, color=LINE_COLOR, lw=1.3, zorder=5)
        p1, p2 = pts[-2], pts[-1]
        ax.annotate("", xy=p2, xytext=p1,
                    arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=6)
        if label:
            if label_pos == "mid":
                mx = (pts[0][0] + pts[1][0]) / 2
                my = pts[0][1] + 0.12
            elif label_pos == "vert":
                mx = pts[0][0] + 0.12
                my = (pts[0][1] + pts[1][1]) / 2
            else:
                mx, my = label_pos
            ax.text(mx, my, label, ha="center", va="bottom", fontsize=7.5, color=LINE_COLOR,
                    bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=7)

    # Clean Forward Transitions (Row 1)
    draw_orthogonal_arrow([(3.0, 9.5), (3.6, 9.5)], "Auto Init")
    draw_orthogonal_arrow([(6.0, 9.5), (6.6, 9.5)], "Token OK")
    draw_orthogonal_arrow([(9.0, 9.5), (9.6, 9.5)], "Play Co-op")
    draw_orthogonal_arrow([(12.0, 9.5), (12.6, 9.5)], "Create / Join")
    draw_orthogonal_arrow([(15.0, 9.5), (15.6, 9.5)], "Both Ready")

    # Row 1 -> Row 2
    draw_orthogonal_arrow([(16.8, 8.9), (16.8, 8.1)], "Spawn", label_pos="vert")

    # Row 2 Transitions
    draw_orthogonal_arrow([(15.6, 7.5), (15.0, 7.5)], "Clear")

    # Rematch: Match Summary -> 3D Lobby Room (straight up)
    draw_orthogonal_arrow([(13.8, 8.1), (13.8, 8.9)], "Rematch", label_pos="vert")

    # Exit to Menu: Match Summary -> Main Menu (completely open space!)
    # Starts at left of Summary (12.6, 7.5), goes straight left to x = 7.8, turns up into Main Menu bottom (7.8, 8.9)
    draw_orthogonal_arrow([(12.6, 7.5), (7.8, 7.5), (7.8, 8.9)], "Exit to Menu", label_pos=(10.2, 7.62))

    # Section Divider Line
    ax.plot([0.8, 18.2], [6.3, 6.3], color="#94A3B8", lw=1.4, ls="--", zorder=2)

    # ── BOTTOM SECTION: WEB PORTAL (REACT / VITE) ───────────────────────────
    ax.text(0.9, 5.85, "WEB PORTAL SCREEN FLOW (React / Tailwind / REST API)",
            ha="left", va="center", fontsize=11.5, fontweight="bold", color="#1E293B", zorder=2)

    # Web Screens
    draw_screen_box(2.4, 3.6, 2.6, 1.4, "Web Login Portal", "JWT RBAC Entry Point\n(Player / Mod / Admin)")
    draw_screen_box(7.8, 4.6, 2.8, 1.2, "Player Dashboard", "Personal Stats & History")
    draw_screen_box(7.8, 2.2, 2.8, 1.2, "Global Leaderboard", "Stage Clear Times & Rank")
    draw_screen_box(14.2, 4.6, 3.0, 1.2, "Moderator Panel", "Live Lobbies & Stat Tuning")
    draw_screen_box(14.2, 2.2, 3.0, 1.2, "Admin Management", "User Accounts & Audit Logs")

    # Web Arrows: Completely separate dedicated corridors
    # Player: straight to Dashboard
    draw_orthogonal_arrow([(3.7, 4.0), (5.2, 4.0), (5.2, 4.6), (6.4, 4.6)], "Player Role")
    # Dashboard <-> Leaderboard
    draw_orthogonal_arrow([(7.8, 4.0), (7.8, 2.8)], "View Rank", label_pos="vert")
    # Moderator: upper corridor at y = 5.4
    draw_orthogonal_arrow([(2.4, 4.3), (2.4, 5.4), (14.2, 5.4), (14.2, 5.2)], "Mod Role", label_pos=(8.3, 5.52))
    # Administrator: lower corridor at y = 1.0
    draw_orthogonal_arrow([(2.4, 2.9), (2.4, 1.0), (14.2, 1.0), (14.2, 1.6)], "Admin Role", label_pos=(8.3, 1.12))

    # Caption
    ax.text(9.5, 0.18, "Figure 3.3: Hold My Hand Screens Navigation Flow (Game Client & Web Portal) — FA26SE047",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r3_fig3_screen_flow.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] Screen Flow diagram generated cleanly (zero collision):", out_path)


# =============================================================================
# 2. PACKAGE DIAGRAM (Report 4 - Figure 4.2) — FIXED NO CUTTING THROUGH BOXES
# =============================================================================
def generate_package_diagram():
    fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
    ax.set_xlim(0, 19)
    ax.set_ylim(0, 12)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Title
    ax.text(9.5, 11.55, "Hold My Hand — Software Package Architecture Diagram (UML 2.5)",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR)

    def draw_uml_package_outer(x, y, w, h, pkg_name):
        tab_w = min(4.0, w * 0.45)
        tab_h = 0.55
        tab = patches.Rectangle((x, y + h), tab_w, tab_h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.6, zorder=2)
        ax.add_patch(tab)
        ax.text(x + tab_w/2, y + h + tab_h/2, pkg_name, ha="center", va="center", fontsize=8.8, fontweight="bold", color=LINE_COLOR, zorder=3)
        body = patches.Rectangle((x, y), w, h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.6, zorder=2)
        ax.add_patch(body)

    def draw_sub_package_box(sp_x, sp_y, sp_w, sp_h, sp_name, sp_items):
        s_tab_w = min(3.0, sp_w * 0.45)
        s_tab = patches.Rectangle((sp_x, sp_y + sp_h), s_tab_w, 0.38, fc=BG_CARD, ec=BORDER_COLOR, lw=1.2, zorder=4)
        ax.add_patch(s_tab)
        ax.text(sp_x + s_tab_w/2, sp_y + sp_h + 0.19, sp_name, ha="center", va="center", fontsize=7.8, fontweight="bold", color=LINE_COLOR, zorder=5)
        s_body = patches.Rectangle((sp_x, sp_y), sp_w, sp_h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.2, zorder=4)
        ax.add_patch(s_body)
        for idx, item in enumerate(sp_items):
            ax.text(sp_x + 0.25, sp_y + sp_h - 0.32 - idx * 0.26, "- " + item, fontsize=7.4, color="#334155", zorder=5)

    # 1. Unity Client Package Outer Box
    draw_uml_package_outer(0.8, 0.6, 7.6, 9.8, "HoldMyHand.UnityClient")

    # 4 Unity Sub-packages with explicit 0.9-unit vertical gaps:
    # Box 1: y in [7.9, 9.3]
    # Box 2: y in [5.6, 7.0] (gap to Box 1: [7.0, 7.9])
    # Box 3: y in [3.3, 4.7] (gap to Box 2: [4.7, 5.6])
    # Box 4: y in [1.0, 2.4] (gap to Box 3: [2.4, 3.3])
    draw_sub_package_box(1.2, 7.9, 6.8, 1.4, "Blocks.Common",
                         ["ApiClient.cs (HTTP REST / JWT Auth)", "ModApiClient.cs (Staff Privileged Client)", "DTOs / JSON Serializers & Constants"])
    draw_sub_package_box(1.2, 5.6, 6.8, 1.4, "Blocks.Sessions",
                         ["Lobby3DManager.cs (Role Swap & State)", "MatchmakingClient.cs (Code Validation)", "LobbyUIController.cs (UI Toolkit Modal)"])
    draw_sub_package_box(1.2, 3.3, 6.8, 1.4, "Blocks.Gameplay.Core",
                         ["CharacterBase.cs (Dad & Child Physics)", "HandHoldingController.cs (Sync Netcode)", "PuzzleInteractable.cs (Stage Logic)"])
    draw_sub_package_box(1.2, 1.0, 6.8, 1.4, "Blocks.Voice",
                         ["VoiceChatManager.cs (Opus PTT Engine)", "VoiceDSPProcessor.cs (Pitch Shifter DSP)", "JitterBuffer.cs (Audio Packet Queue)"])

    # Unity Dependency Arrows (DRAWN STRICTLY WITHIN THE GAPS — NEVER CUTTING BOXES!)
    def draw_gap_arrow(x, y_start, y_end, label):
        ax.plot([x, x], [y_start, y_end], color=LINE_COLOR, lw=1.2, ls="--", zorder=6)
        ax.annotate("", xy=(x, y_end), xytext=(x, y_start),
                    arrowprops=dict(arrowstyle="-|>", mutation_scale=12, color=LINE_COLOR, lw=1.2, shrinkA=0, shrinkB=0), zorder=7)
        ax.text(x + 0.15, (y_start + y_end)/2, label, ha="left", va="center", fontsize=7.2, fontstyle="italic", color=LINE_COLOR,
                bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=8)

    # From Box 2 (top y=7.0) to Box 1 (bottom y=7.9)
    draw_gap_arrow(4.6, 7.0, 7.9, "<<import>>")
    # From Box 3 (top y=4.7) to Box 2 (bottom y=5.6)
    draw_gap_arrow(4.6, 4.7, 5.6, "<<import>>")
    # From Box 4 (top y=2.4) to Box 3 (bottom y=3.3)
    draw_gap_arrow(4.6, 2.4, 3.3, "<<import>>")

    # 2. Backend Package Outer Box
    draw_uml_package_outer(10.6, 0.6, 7.6, 9.8, "HoldMyHand.Backend")

    # 4 Backend Sub-packages with explicit 0.9-unit vertical gaps:
    # Box 1: y in [7.9, 9.3]
    # Box 2: y in [5.6, 7.0] (gap: [7.0, 7.9])
    # Box 3: y in [3.3, 4.7] (gap: [4.7, 5.6])
    # Box 4: y in [1.0, 2.4] (gap: [2.4, 3.3])
    draw_sub_package_box(11.0, 7.9, 6.8, 1.4, "api/ & routes/",
                         ["authRoutes.js (Login, Register, JWKS)", "lobbyRoutes.js (Create, Join, Swap, Ready)", "modRoutes.js (Close, StatTune, SceneItem)"])
    draw_sub_package_box(11.0, 5.6, 6.8, 1.4, "middleware/",
                         ["jwtAuth.js (Verify Bearer Token)", "rbacGuard.js (Require Admin / Moderator)", "rateLimiter.js (DDoS & Brute Force Guard)"])
    draw_sub_package_box(11.0, 3.3, 6.8, 1.4, "services/",
                         ["jwtService.js (Sign & Verify JWT Payload)", "voiceRelayService.js (WSS Binary Relay)", "statsService.js (Dynamic Balancing Logic)"])
    draw_sub_package_box(11.0, 1.0, 6.8, 1.4, "store/ & models/",
                         ["lobbyStore.js (In-Memory Redis Cache)", "User.js (MongoDB Schema & Password Hash)", "AuditLog.js (Security & Action Trail)"])

    # Backend Dependency Arrows (DRAWN STRICTLY WITHIN GAPS — NEVER CUTTING BOXES!)
    # Downward dependencies:
    # From Box 1 (bottom y=7.9) to Box 2 (top y=7.0)
    draw_gap_arrow(14.4, 7.9, 7.0, "<<uses>>")
    # From Box 2 (bottom y=5.6) to Box 3 (top y=4.7)
    draw_gap_arrow(14.4, 5.6, 4.7, "<<uses>>")
    # From Box 3 (bottom y=3.3) to Box 4 (top y=2.4)
    draw_gap_arrow(14.4, 3.3, 2.4, "<<accesses>>")

    # Cross-subsystem communication between Unity and Backend across the central corridor [8.4, 10.6]
    def draw_cross_arrow(y, label, protocol):
        ax.plot([8.4, 10.6], [y, y], color=LINE_COLOR, lw=1.3, zorder=6)
        ax.annotate("", xy=(10.6, y), xytext=(8.4, y),
                    arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=7)
        ax.text(9.5, y + 0.18, label, ha="center", va="bottom", fontsize=7.8, fontweight="bold", color=LINE_COLOR,
                bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=8)
        ax.text(9.5, y - 0.22, protocol, ha="center", va="top", fontsize=7.2, fontstyle="italic", color="#334155",
                bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=8)

    draw_cross_arrow(8.6, "REST API Endpoints", "HTTP / JSON (Auth, Lobby, Mod)")
    draw_cross_arrow(2.2, "Voice Audio Stream", "WSS / Binary PCM (Port 8080)")

    # Caption
    ax.text(9.5, 0.18, "Figure 4.2: Hold My Hand Software Package Structure Diagram (Unity & Backend) — FA26SE047",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r4_fig2_package_diagram.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] Package diagram generated cleanly (zero collision):", out_path)


# =============================================================================
# 3. CLASS DIAGRAM: LOBBY & SESSION MANAGEMENT (Report 4 - Figure 4.4)
# =============================================================================
def generate_class_lobby():
    fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
    ax.set_xlim(0, 19)
    ax.set_ylim(0, 12)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    ax.text(9.5, 11.55, "Hold My Hand — Class Diagram for Account & Lobby Management",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR)

    def draw_uml_class(x, y, w, h, class_name, stereotype, attrs, methods):
        rect = patches.Rectangle((x, y), w, h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.5, zorder=2)
        ax.add_patch(rect)
        hdr_h = 0.8
        if stereotype:
            ax.text(x + w/2, y + h - 0.25, stereotype, ha="center", va="center", fontsize=7.2, fontstyle="italic", color="#475569", zorder=3)
            ax.text(x + w/2, y + h - 0.55, class_name, ha="center", va="center", fontsize=9.2, fontweight="bold", color=LINE_COLOR, zorder=3)
        else:
            ax.text(x + w/2, y + h - hdr_h/2, class_name, ha="center", va="center", fontsize=9.5, fontweight="bold", color=LINE_COLOR, zorder=3)
        ax.plot([x, x + w], [y + h - hdr_h, y + h - hdr_h], color=BORDER_COLOR, lw=1.2, zorder=3)
        
        cur_y = y + h - hdr_h - 0.28
        for attr in attrs:
            ax.text(x + 0.2, cur_y, attr, ha="left", va="center", fontsize=7.8, color="#1E293B", zorder=3)
            cur_y -= 0.28
        sep_y = cur_y + 0.1
        ax.plot([x, x + w], [sep_y, sep_y], color=BORDER_COLOR, lw=1.2, zorder=3)
        cur_y = sep_y - 0.28
        for method in methods:
            ax.text(x + 0.2, cur_y, method, ha="left", va="center", fontsize=7.8, color=LINE_COLOR, zorder=3)
            cur_y -= 0.28

    # ApiClient (Top-Left)
    draw_uml_class(0.8, 6.2, 7.8, 4.6, "ApiClient", "<<singleton / network client>>",
                   ["-_baseUrl: string = \"https://api.holdmyhand.online\"",
                    "-_authToken: string",
                    "-_isInitialized: bool",
                    "-_httpClient: HttpClient"],
                   ["+Instance: ApiClient { get; }",
                    "+SetAuthToken(token: string): void",
                    "+GetAsync<T>(endpoint: string): Task<T>",
                    "+PostAsync<T>(endpoint: string, payload: object): Task<T>",
                    "+DeleteAsync<T>(endpoint: string): Task<T>",
                    "+HandleNetworkError(ex: Exception): void"])

    # ModApiClient (Bottom-Left - inherits ApiClient)
    draw_uml_class(0.8, 1.0, 7.8, 4.2, "ModApiClient", "<<staff privileged client>>",
                   ["-_modRole: string = \"Moderator\"",
                    "-_activeLobbyCache: List<LobbyDTO>"],
                   ["+CloseLobbyAsync(code: string, reason: string): Task<CloseResp>",
                    "+AdjustPlayerStatsAsync(code: string, stats: StatsDTO): Task<bool>",
                    "+ManageGameSceneAsync(stageId: string, cfg: SceneDTO): Task<bool>",
                    "+ManageItemPropsAsync(itemId: string, prop: ItemDTO): Task<bool>",
                    "+KickPlayerAsync(code: string, playerId: string): Task<bool>"])

    # Inheritance Arrow: ModApiClient -> ApiClient (Hollow triangle)
    ax.plot([4.7, 4.7], [5.2, 6.2], color=LINE_COLOR, lw=1.3, zorder=4)
    tri = patches.Polygon([[4.7, 6.2], [4.55, 5.85], [4.85, 5.85]], closed=True, fc="white", ec=LINE_COLOR, lw=1.3, zorder=5)
    ax.add_patch(tri)
    ax.text(4.85, 5.5, "<<extends>>", fontsize=7.5, fontstyle="italic", color=LINE_COLOR, zorder=5)

    # Lobby3DManager (Top-Right)
    draw_uml_class(10.4, 5.8, 7.8, 5.0, "Lobby3DManager", "<<MonoBehaviour / UI Controller>>",
                   ["-_currentLobby: LobbyDTO",
                    "-_isDadRole: bool",
                    "-_isReady: bool",
                    "-_apiClient: ApiClient",
                    "-_dadAvatar3D: GameObject",
                    "-_childAvatar3D: GameObject",
                    "-_roleSwapBtn: Button"],
                   ["+InitializeLobbyUI(): void",
                    "+OnCreateLobbyClicked(): Task<void>",
                    "+OnJoinLobbyClicked(code: string): Task<void>",
                    "+OnRoleSwapClicked(): Task<void>",
                    "+OnReadyClicked(): Task<void>",
                    "+UpdateLobbyDisplay(lobby: LobbyDTO): void",
                    "+OnRoomClosedByMod(reason: string): void"])

    # Association Arrow: Lobby3DManager -> ApiClient
    ax.plot([10.4, 8.6], [8.5, 8.5], color=LINE_COLOR, lw=1.3, zorder=4)
    ax.annotate("", xy=(8.6, 8.5), xytext=(10.4, 8.5),
                arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=5)
    ax.text(9.5, 8.65, "<<uses>>", ha="center", va="bottom", fontsize=7.8, fontstyle="italic", color=LINE_COLOR,
            bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=6)

    # LobbyDTO (Bottom-Right)
    draw_uml_class(10.4, 1.0, 7.8, 4.2, "LobbyDTO", "<<data transfer object>>",
                   ["+lobbyCode: string (6 uppercase chars)",
                    "+status: string ('WAITING' | 'READY' | 'ACTIVE' | 'CLOSED')",
                    "+hostPlayer: PlayerSlot",
                    "+clientPlayer: PlayerSlot",
                    "+createdAt: DateTime"],
                   ["+IsFull(): bool",
                    "+BothPlayersReady(): bool",
                    "+GetRoleByPlayerId(playerId: string): Role",
                    "+SwapRoles(): void"])

    # Aggregation Arrow: Lobby3DManager holds LobbyDTO (Hollow diamond)
    ax.plot([14.3, 14.3], [5.8, 5.2], color=LINE_COLOR, lw=1.3, zorder=4)
    diamond = patches.Polygon([[14.3, 5.8], [14.15, 5.6], [14.3, 5.4], [14.45, 5.6]], closed=True, fc="white", ec=LINE_COLOR, lw=1.3, zorder=5)
    ax.add_patch(diamond)
    ax.text(14.45, 5.5, "1 holds", fontsize=7.5, fontstyle="italic", color=LINE_COLOR, zorder=5)

    ax.text(9.5, 0.18, "Figure 4.4: Class Diagram for Account & Lobby Management (Blocks.Common & Blocks.Sessions) — FA26SE047",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r4_fig4_class_lobby.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] Class Lobby diagram generated:", out_path)


# =============================================================================
# 4. CLASS DIAGRAM: VOICE CHAT & NETCODE DSP (Report 4 - Figure 4.7)
# =============================================================================
def generate_class_voice():
    fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
    ax.set_xlim(0, 19)
    ax.set_ylim(0, 12)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    ax.text(9.5, 11.55, "Hold My Hand — Class Diagram for Voice Streaming & Audio DSP Engine",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR)

    def draw_uml_class(x, y, w, h, class_name, stereotype, attrs, methods):
        rect = patches.Rectangle((x, y), w, h, fc=BG_CARD, ec=BORDER_COLOR, lw=1.5, zorder=2)
        ax.add_patch(rect)
        hdr_h = 0.8
        if stereotype:
            ax.text(x + w/2, y + h - 0.25, stereotype, ha="center", va="center", fontsize=7.2, fontstyle="italic", color="#475569", zorder=3)
            ax.text(x + w/2, y + h - 0.55, class_name, ha="center", va="center", fontsize=9.2, fontweight="bold", color=LINE_COLOR, zorder=3)
        else:
            ax.text(x + w/2, y + h - hdr_h/2, class_name, ha="center", va="center", fontsize=9.5, fontweight="bold", color=LINE_COLOR, zorder=3)
        ax.plot([x, x + w], [y + h - hdr_h, y + h - hdr_h], color=BORDER_COLOR, lw=1.2, zorder=3)
        
        cur_y = y + h - hdr_h - 0.28
        for attr in attrs:
            ax.text(x + 0.2, cur_y, attr, ha="left", va="center", fontsize=7.8, color="#1E293B", zorder=3)
            cur_y -= 0.28
        sep_y = cur_y + 0.1
        ax.plot([x, x + w], [sep_y, sep_y], color=BORDER_COLOR, lw=1.2, zorder=3)
        cur_y = sep_y - 0.28
        for method in methods:
            ax.text(x + 0.2, cur_y, method, ha="left", va="center", fontsize=7.8, color=LINE_COLOR, zorder=3)
            cur_y -= 0.28

    # VoiceChatManager (Left)
    draw_uml_class(0.8, 2.0, 7.8, 8.8, "VoiceChatManager", "<<MonoBehaviour / Audio Client>>",
                   ["-_ws: ClientWebSocket",
                    "-_micClip: AudioClip",
                    "-_jitterBuffer: Queue<byte[]>",
                    "-_isPTT: bool = true",
                    "-_pttKey: KeyCode = KeyCode.V",
                    "-_isAutoGate: bool = false",
                    "-_sampleRate: int = 16000",
                    "-_frameSamples: int = 320",
                    "-_dspProcessor: VoiceDSPProcessor",
                    "-_playerRole: Role (Dad | Child)"],
                   ["+ConnectVoiceAsync(url: string, lobbyId: string, token: string): Task<void>",
                    "+StartRecording(): void",
                    "+StopRecording(): void",
                    "+SendAudioFrame(pcmSamples: short[]): Task<void>",
                    "+ReceiveLoop(): Task<void>",
                    "+ProcessJitterBuffer(): void",
                    "+SetMute(isMuted: bool): void",
                    "+OnApplicationQuit(): void"])

    # VoiceDSPProcessor (Top-Right)
    draw_uml_class(10.4, 6.8, 7.8, 4.0, "VoiceDSPProcessor", "<<static DSP engine>>",
                   ["+DAD_SEMITONES: float = -4.0f",
                    "+CHILD_SEMITONES: float = +5.0f",
                    "+SAMPLE_RATE: int = 16000",
                    "+FRAME_SIZE: int = 320"],
                   ["+ProcessVoice(samples: float[], role: Role): float[]",
                    "+ShiftPitch(samples: float[], semitones: float): float[]",
                    "+ApplyFormantEnvelope(samples: float[], factor: float): float[]",
                    "+ApplyNoiseGate(samples: float[], threshold: float): float[]"])

    # VoiceRelayService (Bottom-Right)
    draw_uml_class(10.4, 2.0, 7.8, 4.2, "VoiceRelayService", "<<Node.js WebSocket Server>>",
                   ["-_rooms: Map<string, Set<WebSocket>>",
                    "-_jwksUrl: string = 'https://auth.holdmyhand.online/.well-known/jwks.json'",
                    "+FRAME_SIZE: int = 648 (8B Header + 640B PCM)"],
                   ["+attachVoiceRelay(httpServer: Server): void",
                    "+handleAuthFrame(ws: WebSocket, jsonFrame: object): Promise<bool>",
                    "+relayBinaryAudio(senderWs: WebSocket, lobbyId: string, payload: Buffer): void",
                    "+handleClientDisconnect(ws: WebSocket): void"])

    # Associations
    ax.plot([8.6, 10.4], [8.8, 8.8], color=LINE_COLOR, lw=1.3, zorder=4)
    ax.annotate("", xy=(10.4, 8.8), xytext=(8.6, 8.8),
                arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=5)
    ax.text(9.5, 8.95, "applies DSP", ha="center", va="bottom", fontsize=7.8, fontstyle="italic", color=LINE_COLOR,
            bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=6)

    ax.plot([8.6, 10.4], [4.1, 4.1], color=LINE_COLOR, lw=1.3, zorder=4)
    ax.annotate("", xy=(10.4, 4.1), xytext=(8.6, 4.1),
                arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=5)
    ax.text(9.5, 4.25, "streams WSS", ha="center", va="bottom", fontsize=7.8, fontstyle="italic", color=LINE_COLOR,
            bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=6)

    ax.text(9.5, 0.18, "Figure 4.7: Class Diagram for Voice Chat Streaming & DSP Engine (VOICE-001–004) — FA26SE047",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r4_fig7_class_voice.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] Class Voice diagram generated:", out_path)


# =============================================================================
# HELPER: GENERIC SEQUENCE DIAGRAM RENDERER
# =============================================================================
def draw_sequence_diagram(out_file, title, caption, lifelines, steps):
    fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
    ax.set_xlim(0, 19)
    ax.set_ylim(0, 12)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    ax.text(9.5, 11.55, title, ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR)
    boundary = patches.Rectangle((0.5, 0.4), 18.0, 10.8, fc=BG_CARD, ec=BORDER_COLOR, lw=1.8, zorder=1)
    ax.add_patch(boundary)

    for x, label in lifelines:
        box = patches.Rectangle((x - 1.25, 9.8), 2.5, 0.7, fc=BG_CARD, ec=BORDER_COLOR, lw=1.4, zorder=3)
        ax.add_patch(box)
        ax.text(x, 10.15, label, ha="center", va="center", fontsize=8.5, fontweight="bold", color=LINE_COLOR, zorder=4)
        ax.plot([x, x], [0.8, 9.8], color="#64748B", lw=1.1, ls="--", zorder=2)

    for x1, x2, y, label, msg_type in steps:
        if msg_type == "self":
            loop_w = 0.6
            loop_h = 0.35
            ax.plot([x1, x1 + loop_w, x1 + loop_w, x1], [y, y, y - loop_h, y - loop_h], color=LINE_COLOR, lw=1.3, zorder=5)
            ax.annotate("", xy=(x1, y - loop_h), xytext=(x1 + 0.15, y - loop_h),
                        arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=6)
            ax.text(x1 + loop_w + 0.12, y - loop_h/2, label, ha="left", va="center", fontsize=7.8, color=LINE_COLOR,
                    bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=7)
        else:
            ls = "--" if msg_type == "reply" else "-"
            ax.plot([x1, x2], [y, y], color=LINE_COLOR, lw=1.3, ls=ls, zorder=5)
            ax.annotate("", xy=(x2, y), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="-|>", mutation_scale=MUTATION_SCALE, color=LINE_COLOR, lw=1.3, shrinkA=0, shrinkB=0), zorder=6)
            mx = (x1 + x2) / 2
            ax.text(mx, y + 0.12, label, ha="center", va="bottom", fontsize=7.8, color=LINE_COLOR,
                    bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=1.0), zorder=7)

    ax.text(9.5, 0.18, caption, ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, out_file)
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print(f"[OK] Sequence diagram generated: {out_path}")


# =============================================================================
# 5. SEQUENCE DIAGRAM: LOGIN & JWT ISSUANCE (Figure 4.5)
# =============================================================================
def generate_seq_login():
    lifelines = [
        (2.0, "Player (User)"),
        (5.5, "LoginScreen (UI)"),
        (9.5, "ApiClient.cs"),
        (13.5, "AuthService (API)"),
        (17.0, "MongoDB Atlas"),
    ]
    steps = [
        (2.0, 5.5, 9.1, "1: Enter email & password, click 'Login'", "sync"),
        (5.5, 9.5, 8.2, "2: PostAsync('/api/auth/login', credentials)", "sync"),
        (9.5, 13.5, 7.3, "3: HTTP POST /api/auth/login", "sync"),
        (13.5, 17.0, 6.4, "4: findOne({ email: req.body.email })", "sync"),
        (17.0, 13.5, 5.5, "5: User record & bcrypt hash returned", "reply"),
        (13.5, 13.5, 4.6, "6: Compare bcrypt hash & sign JWT token", "self"),
        (13.5, 9.5, 3.7, "7: 200 OK { token, role, userId, username }", "reply"),
        (9.5, 5.5, 2.8, "8: Save token into PlayerPrefs & set ApiClient state", "reply"),
        (5.5, 2.0, 1.9, "9: Transition to LobbyBrowserScreen UI", "reply"),
    ]
    draw_sequence_diagram(
        "r4_fig5_seq_login.png",
        "Hold My Hand — Sequence Diagram for User Login and JWT Authentication",
        "Figure 4.5: Sequence Diagram for User Login and JWT Authentication — FA26SE047",
        lifelines, steps
    )


# =============================================================================
# 6. SEQUENCE DIAGRAM: CREATE & JOIN LOBBY (Figure 4.6)
# =============================================================================
def generate_seq_lobby():
    lifelines = [
        (1.8, "Player 1 (Dad)"),
        (5.2, "Player 2 (Child)"),
        (9.2, "Lobby3DManager"),
        (13.2, "LobbyService (API)"),
        (17.0, "LobbyStore (Redis)"),
    ]
    steps = [
        (1.8, 9.2, 9.1, "1: Click 'Create Lobby' (Host)", "sync"),
        (9.2, 13.2, 8.2, "2: POST /api/game/lobby/create (Bearer JWT)", "sync"),
        (13.2, 17.0, 7.3, "3: Generate 6-char code & init room { host: 'Dad' }", "sync"),
        (13.2, 9.2, 6.4, "4: 200 OK { lobbyCode: 'AB12CD', role: 'Dad' }", "reply"),
        (9.2, 1.8, 5.5, "5: Display code 'AB12CD' in 3D Room", "reply"),
        (5.2, 9.2, 4.6, "6: Enter 'AB12CD' & click 'Join Lobby'", "sync"),
        (9.2, 13.2, 3.7, "7: POST /api/game/lobby/join { lobbyCode: 'AB12CD' }", "sync"),
        (13.2, 17.0, 2.8, "8: Validate slot availability & assign role 'Child'", "sync"),
        (13.2, 9.2, 1.9, "9: 200 OK { status: 'WAITING', role: 'Child' }", "reply"),
        (9.2, 1.8, 1.1, "10: Synchronize lobby state & enable 'Ready' button", "reply"),
    ]
    draw_sequence_diagram(
        "r4_fig6_seq_lobby.png",
        "Hold My Hand — Sequence Diagram for 2-Player Matchmaking and Role Assignment",
        "Figure 4.6: Sequence Diagram for 2-Player Matchmaking and Role Assignment — FA26SE047",
        lifelines, steps
    )


# =============================================================================
# 7. SEQUENCE DIAGRAM: VOICE CHAT RELAY (Figure 4.8)
# =============================================================================
def generate_seq_voice():
    lifelines = [
        (1.6, "Player A (Dad)"),
        (5.0, "VoiceChatMgr A"),
        (8.8, "VoiceDSPProcessor"),
        (12.6, "WSS Relay Server"),
        (15.8, "VoiceChatMgr B"),
        (18.0, "Player B"),
    ]
    steps = [
        (5.0, 12.6, 9.2, "1: Connect WSS /voice & send auth frame (JWT)", "sync"),
        (12.6, 5.0, 8.4, "2: 200 OK: Handshake accepted { type: 'ready' }", "reply"),
        (15.8, 12.6, 7.6, "3: Client B connects & authenticates", "sync"),
        (1.6, 5.0, 6.8, "4: Dad holds PTT Key 'V' -> capture PCM (16kHz)", "sync"),
        (5.0, 8.8, 6.0, "5: ProcessVoice(samples, role: Dad)", "sync"),
        (8.8, 5.0, 5.2, "6: ShiftPitch(-4 semitones) & noise gate applied", "reply"),
        (5.0, 12.6, 4.4, "7: Send binary audio packet (648B: 8B hdr + 640B PCM)", "sync"),
        (12.6, 15.8, 3.6, "8: Relay audio payload to peer in same room", "sync"),
        (15.8, 18.0, 2.8, "9: JitterBuffer enqueue -> AudioSource.Play()", "sync"),
        (1.6, 5.0, 2.0, "10: Dad releases PTT Key 'V' -> send silence end frame", "sync"),
    ]
    draw_sequence_diagram(
        "r4_fig8_seq_voice.png",
        "Hold My Hand — Sequence Diagram for Voice Chat Handshake & Binary Audio Streaming",
        "Figure 4.8: Sequence Diagram for Voice Chat Handshake & Binary Audio Streaming — FA26SE047",
        lifelines, steps
    )


# =============================================================================
# 8. SEQUENCE DIAGRAM: MODERATOR FORCE CLOSE (Figure 4.9)
# =============================================================================
def generate_seq_mod_close():
    lifelines = [
        (1.8, "Moderator (Web)"),
        (6.0, "ModService (API)"),
        (10.5, "LobbyStore (Redis)"),
        (14.8, "AuditLog (MongoDB)"),
        (17.8, "Game Clients"),
    ]
    steps = [
        (1.8, 6.0, 9.1, "1: POST /api/mod/lobby/:code/close { reason: 'Idle > 30m' }", "sync"),
        (6.0, 6.0, 8.1, "2: Verify Bearer JWT & validate role == 'Moderator'", "self"),
        (6.0, 10.5, 7.1, "3: getLobby(code) -> check status != 'CLOSED'", "sync"),
        (10.5, 6.0, 6.1, "4: Return current lobby data", "reply"),
        (6.0, 10.5, 5.1, "5: update(code, { status: 'CLOSED', closedBy: modId })", "sync"),
        (6.0, 14.8, 4.1, "6: appendAuditRecord({ action: 'FORCE_CLOSE', modId, code })", "sync"),
        (6.0, 1.8, 3.1, "7: 200 OK { success: true, currentStatus: 'CLOSED' }", "reply"),
        (6.0, 17.8, 2.0, "8: Broadcast WebSocket room termination event to players", "sync"),
        (17.8, 17.8, 1.1, "9: Show 'Session terminated by moderator' modal & exit", "self"),
    ]
    draw_sequence_diagram(
        "r4_fig9_seq_mod_close.png",
        "Hold My Hand — Sequence Diagram for Moderator Force Close Session (MOD-001)",
        "Figure 4.9: Sequence Diagram for Moderator Force Close Session (MOD-001) — FA26SE047",
        lifelines, steps
    )


# =============================================================================
# MAIN RUNNER
# =============================================================================
if __name__ == "__main__":
    print("[INFO] Regenerating all standard diagrams according to user style...")
    generate_screen_flow()
    generate_package_diagram()
    generate_class_lobby()
    generate_class_voice()
    generate_seq_login()
    generate_seq_lobby()
    generate_seq_voice()
    generate_seq_mod_close()
    print("[SUCCESS] All diagrams regenerated cleanly!")
