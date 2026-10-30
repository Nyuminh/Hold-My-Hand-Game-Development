import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from PIL import Image

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)

# Copy logo if exists
logo_src = os.path.join(OUT_DIR, "Report1_Project Introduction_img_0.png")
logo_dst = os.path.join(OUT_DIR, "fpt_logo.png")
if os.path.exists(logo_src) and not os.path.exists(logo_dst):
    img = Image.open(logo_src)
    img.save(logo_dst)
    print("Copied fpt_logo.png")

# Palette
PRIMARY = "#1E3A8A"    # Deep Blue
SECONDARY = "#2563EB"  # Royal Blue
ACCENT = "#D97706"     # Amber
SUCCESS = "#059669"    # Emerald Green
DANGER = "#DC2626"     # Red
BG_LIGHT = "#F8FAFC"   # Slate 50
BOX_BG = "#EFF6FF"     # Blue 50
BOX_BORDER = "#3B82F6" # Blue 500
TEXT_DARK = "#0F172A"  # Slate 900
TEXT_MUTED = "#475569" # Slate 600

def draw_box(ax, x, y, w, h, title, subtitle="", bg=BOX_BG, border=BOX_BORDER, radius=0.03, lw=1.5):
    box = patches.FancyBboxPatch((x, y), w, h, boxstyle=f"round,pad={radius}",
                                fc=bg, ec=border, lw=lw, zorder=2)
    ax.add_patch(box)
    if subtitle:
        ax.text(x + w/2, y + h*0.62, title, ha="center", va="center", fontsize=9, fontweight="bold", color=TEXT_DARK, zorder=3)
        ax.text(x + w/2, y + h*0.32, subtitle, ha="center", va="center", fontsize=7.5, color=TEXT_MUTED, zorder=3)
    else:
        ax.text(x + w/2, y + h/2, title, ha="center", va="center", fontsize=9, fontweight="bold", color=TEXT_DARK, zorder=3)
    return box

def draw_arrow(ax, x1, y1, x2, y2, label="", color="#475569", lw=1.5, style="->"):
    ax.annotate("", xy=(x2, y2), xytext=(x1, y1),
                arrowprops=dict(arrowstyle=style, color=color, lw=lw, shrinkA=5, shrinkB=5), zorder=4)
    if label:
        mx, my = (x1 + x2)/2, (y1 + y2)/2
        ax.text(mx, my + 0.02, label, ha="center", va="bottom", fontsize=7.5, fontweight="semibold",
                color=color, bbox=dict(boxstyle="round,pad=0.2", fc="white", ec="none", alpha=0.85), zorder=5)

# ==========================================
# DIAGRAM 1: Context Diagram (Report 3)
# ==========================================
def gen_r3_context():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # System boundary box
    sys_box = patches.FancyBboxPatch((3.2, 1.0), 3.6, 4.0, boxstyle="round,pad=0.1",
                                     fc="#F1F5F9", ec="#1E3A8A", lw=2.5, ls="--", zorder=1)
    ax.add_patch(sys_box)
    ax.text(5.0, 4.75, "HOLD MY HAND SYSTEM (FA26SE047)", ha="center", fontsize=11, fontweight="bold", color="#1E3A8A")

    # Internal sub-systems
    draw_box(ax, 3.5, 3.6, 3.0, 0.8, "Unity 6 Game Client", "URP, Netcode, VoiceChatManager, ApiClient", bg="#DBEAFE", border="#2563EB")
    draw_box(ax, 3.5, 2.3, 3.0, 0.8, "Backend REST & Voice Relay", "Node.js/Express, JWT, WSS /voice (Vercel)", bg="#DCFCE7", border="#16A34A")
    draw_box(ax, 3.5, 1.2, 3.0, 0.65, "Web Portal & Admin", "ReactJS, Tailwind, Axios", bg="#FEF3C7", border="#D97706")

    # Internal links
    draw_arrow(ax, 5.0, 3.6, 5.0, 3.1, "REST / WSS", color="#2563EB")
    draw_arrow(ax, 5.0, 2.3, 5.0, 1.85, "REST API", color="#16A34A")

    # External Actors / Systems
    # Left: Players
    draw_box(ax, 0.4, 4.2, 1.8, 1.1, "Player 1 (Dad)", "Desktop PC (Win64)\nKeyboard / Mic", bg="#FEE2E2", border="#DC2626")
    draw_box(ax, 0.4, 2.4, 1.8, 1.1, "Player 2 (Child)", "Desktop PC (Win64)\nKeyboard / Mic", bg="#FEE2E2", border="#DC2626")
    draw_box(ax, 0.4, 0.6, 1.8, 1.1, "Web User (Player)", "Browser (Chrome)\nDashboard & Stats", bg="#FEF9C3", border="#CA8A04")

    # Right: Admin / External Services
    draw_box(ax, 7.8, 4.2, 1.8, 1.1, "Unity Gaming Services", "Unity Lobby SDK\nJWKS Auth Verification", bg="#EDE9FE", border="#7C3AED")
    draw_box(ax, 7.8, 2.4, 1.8, 1.1, "Cloud Databases", "MongoDB Atlas (Data)\nRedis / Upstash (Lobby)", bg="#F3E8FF", border="#9333EA")
    draw_box(ax, 7.8, 0.6, 1.8, 1.1, "Moderator / Admin", "Browser Management\nForce Close & Ban", bg="#FFE4E6", border="#E11D48")

    # Connect external to system
    draw_arrow(ax, 2.2, 4.75, 3.5, 4.15, "Game Input / Voice", color="#DC2626")
    draw_arrow(ax, 2.2, 2.95, 3.5, 3.85, "Game Input / Voice", color="#DC2626")
    draw_arrow(ax, 2.2, 1.15, 3.5, 1.5, "HTTP REST", color="#CA8A04")

    draw_arrow(ax, 6.5, 4.0, 7.8, 4.6, "Lobby Membership", color="#7C3AED")
    draw_arrow(ax, 6.5, 2.7, 7.8, 2.95, "CRUD / Session State", color="#9333EA")
    draw_arrow(ax, 7.8, 1.15, 6.5, 1.5, "Mod / Admin Actions", color="#E11D48")

    plt.title("Figure 3.1: Hold My Hand System Context Diagram", fontsize=12, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r3_fig1_context_diagram.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 2: Use Case Diagram (Report 3)
# ==========================================
def gen_r3_usecases():
    fig, ax = plt.subplots(figsize=(11, 7), dpi=200)
    ax.set_xlim(0, 11)
    ax.set_ylim(0, 7)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Boundary
    sys_box = patches.FancyBboxPatch((2.2, 0.4), 6.6, 6.2, boxstyle="round,pad=0.1",
                                     fc="#FAFAFA", ec="#1E3A8A", lw=2, zorder=1)
    ax.add_patch(sys_box)
    ax.text(5.5, 6.35, "HOLD MY HAND — SYSTEM USE CASES", ha="center", fontsize=11, fontweight="bold", color="#1E3A8A")

    # Actors: Left (Guest, Player), Right (Moderator, Admin)
    # Actor icons as circles + text
    def draw_actor(ax, x, y, name, role):
        head = plt.Circle((x, y + 0.3), 0.18, fc="#DBEAFE", ec="#1E3A8A", lw=1.5, zorder=4)
        ax.add_patch(head)
        ax.plot([x, x], [y + 0.12, y - 0.2], color="#1E3A8A", lw=1.5, zorder=4)
        ax.plot([x - 0.2, x + 0.2], [y + 0.02, y + 0.02], color="#1E3A8A", lw=1.5, zorder=4)
        ax.plot([x, x - 0.15], [y - 0.2, y - 0.45], color="#1E3A8A", lw=1.5, zorder=4)
        ax.plot([x, x + 0.15], [y - 0.2, y - 0.45], color="#1E3A8A", lw=1.5, zorder=4)
        ax.text(x, y - 0.65, f"<<actor>>\n{name}\n({role})", ha="center", fontsize=7.5, fontweight="bold", color="#0F172A")

    draw_actor(ax, 0.9, 5.2, "Guest", "Unauthenticated")
    draw_actor(ax, 0.9, 2.5, "Player", "Authenticated")
    draw_actor(ax, 10.1, 4.8, "Moderator", "Staff Role")
    draw_actor(ax, 10.1, 1.8, "Admin", "System Admin")

    # Use cases (ellipses)
    ucs = [
        # (x, y, text, id)
        (3.4, 5.6, "UC01: Register Account", "#EFF6FF", "#2563EB"),
        (3.4, 4.8, "UC02: Login (JWT)", "#EFF6FF", "#2563EB"),
        (4.5, 4.0, "UC04: Create Lobby", "#ECFDF5", "#059669"),
        (4.5, 3.2, "UC05: Join Lobby", "#ECFDF5", "#059669"),
        (6.5, 4.0, "UC06: Swap Role (Dad/Child)", "#ECFDF5", "#059669"),
        (6.5, 3.2, "UC07: Ready & Start Game", "#ECFDF5", "#059669"),
        (4.2, 2.2, "UC10: Co-op Puzzle Solve", "#FEF3C7", "#D97706"),
        (6.5, 2.2, "UC11: Boss Encounter", "#FEF3C7", "#D97706"),
        (5.4, 1.4, "UC12: Voice Chat (PTT/Auto)", "#FEF3C7", "#D97706"),
        (5.5, 5.6, "UC14: Force Close Lobby", "#FEE2E2", "#DC2626"),
        (7.5, 5.0, "UC15: Dynamic Stat Tune", "#FEE2E2", "#DC2626"),
        (7.5, 1.2, "UC16: User & Ban Mgmt", "#F3E8FF", "#7C3AED"),
        (7.5, 0.6, "UC18: Audit Log Review", "#F3E8FF", "#7C3AED"),
    ]

    uc_patches = {}
    for x, y, label, bg, border in ucs:
        ell = patches.FancyBboxPatch((x - 0.95, y - 0.25), 1.9, 0.5, boxstyle="round,pad=0.15",
                                    fc=bg, ec=border, lw=1.2, zorder=2)
        ax.add_patch(ell)
        ax.text(x, y, label, ha="center", va="center", fontsize=7.5, fontweight="semibold", color="#0F172A", zorder=3)
        uc_patches[label.split(":")[0]] = (x, y)

    # Actor connections
    def link(ax, x1, y1, x2, y2, color="#64748B"):
        ax.plot([x1, x2], [y1, y2], color=color, lw=1, ls="-", zorder=1)

    # Guest links
    link(ax, 1.2, 5.2, 2.45, 5.6)
    link(ax, 1.2, 5.2, 2.45, 4.8)

    # Player links
    for ucid in ["UC02", "UC04", "UC05", "UC06", "UC07", "UC10", "UC11", "UC12"]:
        tx, ty = uc_patches[ucid]
        link(ax, 1.2, 2.5, tx - 0.9, ty, color="#2563EB")

    # Moderator links
    for ucid in ["UC14", "UC15"]:
        tx, ty = uc_patches[ucid]
        link(ax, 9.8, 4.8, tx + 0.9, ty, color="#DC2626")

    # Admin links
    for ucid in ["UC14", "UC16", "UC18"]:
        tx, ty = uc_patches[ucid]
        link(ax, 9.8, 1.8, tx + 0.9, ty, color="#7C3AED")

    plt.title("Figure 3.2: Hold My Hand Use Case Diagram (System Functional Groups)", fontsize=12, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r3_fig2_usecase_diagram.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 3: Screens Flow Diagram (Report 3)
# ==========================================
def gen_r3_screen_flow():
    fig, ax = plt.subplots(figsize=(11, 6.5), dpi=200)
    ax.set_xlim(0, 11)
    ax.set_ylim(0, 6.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Game Client Screen Flow (Top half)
    ax.text(0.5, 6.1, "GAME CLIENT SCREEN FLOW (Unity 6)", fontsize=10, fontweight="bold", color="#1E3A8A")
    draw_box(ax, 0.5, 4.8, 1.5, 0.8, "Splash Screen", "Logo & Version Check", bg="#E2E8F0", border="#64748B")
    draw_box(ax, 2.5, 4.8, 1.5, 0.8, "Main Menu", "Play / Settings / Quit", bg="#DBEAFE", border="#2563EB")
    draw_box(ax, 2.5, 3.5, 1.5, 0.7, "Login / Register", "JWT Authentication", bg="#FEF3C7", border="#D97706")
    draw_box(ax, 4.6, 4.8, 1.6, 0.8, "Lobby Browser", "Public List / Code Search", bg="#DBEAFE", border="#2563EB")
    draw_box(ax, 6.8, 4.7, 1.8, 1.0, "Lobby 3D Room", "Role Swap (Dad/Child)\nReady & Mic Settings", bg="#DCFCE7", border="#16A34A")
    draw_box(ax, 9.2, 4.8, 1.5, 0.8, "Loading Screen", "Scene Pre-load & Net", bg="#E2E8F0", border="#64748B")
    draw_box(ax, 9.2, 3.2, 1.5, 1.0, "Gameplay Scene", "Puzzle Level 1 & 2\nBoss Fight (Asymmetric)", bg="#FEE2E2", border="#DC2626")
    draw_box(ax, 6.8, 3.2, 1.8, 0.8, "Result / Summary", "Win/Lose & Return Lobby", bg="#F3E8FF", border="#9333EA")

    # Flow arrows Game
    draw_arrow(ax, 2.0, 5.2, 2.5, 5.2, "")
    draw_arrow(ax, 3.25, 4.8, 3.25, 4.2, "Require Auth")
    draw_arrow(ax, 4.0, 3.85, 4.6, 5.0, "Token OK")
    draw_arrow(ax, 4.0, 5.2, 4.6, 5.2, "Create / Join")
    draw_arrow(ax, 6.2, 5.2, 6.8, 5.2, "Enter Room")
    draw_arrow(ax, 8.6, 5.2, 9.2, 5.2, "Both Ready")
    draw_arrow(ax, 9.95, 4.8, 9.95, 4.2, "Spawn")
    draw_arrow(ax, 9.2, 3.7, 8.6, 3.6, "Finish")
    draw_arrow(ax, 6.8, 3.6, 4.0, 4.9, "Re-lobby", color="#9333EA")

    # Divider
    ax.plot([0.5, 10.5], [2.7, 2.7], color="#CBD5E1", lw=1.5, ls=":")

    # Web Portal Screen Flow (Bottom half)
    ax.text(0.5, 2.4, "WEB PORTAL SCREEN FLOW (ReactJS)", fontsize=10, fontweight="bold", color="#D97706")
    draw_box(ax, 0.5, 1.1, 1.6, 0.9, "Web Login", "Player / Mod / Admin", bg="#FEF3C7", border="#D97706")
    draw_box(ax, 2.8, 1.1, 1.8, 0.9, "Player Dashboard", "Match History & Badges", bg="#DBEAFE", border="#2563EB")
    draw_box(ax, 5.3, 1.1, 1.8, 0.9, "Leaderboard", "Level Clear Times", bg="#DBEAFE", border="#2563EB")
    draw_box(ax, 7.8, 1.3, 2.2, 0.8, "Mod Lobby Monitor", "Active Sessions & Force Close", bg="#FEE2E2", border="#DC2626")
    draw_box(ax, 7.8, 0.3, 2.2, 0.8, "Admin Management", "User Ban & Audit Trail", bg="#F3E8FF", border="#7C3AED")

    # Flow arrows Web
    draw_arrow(ax, 2.1, 1.55, 2.8, 1.55, "Player Role")
    draw_arrow(ax, 4.6, 1.55, 5.3, 1.55, "View Ranks")
    draw_arrow(ax, 2.1, 1.7, 7.8, 1.7, "Mod Role", color="#DC2626")
    draw_arrow(ax, 2.1, 1.3, 7.8, 0.7, "Admin Role", color="#7C3AED")

    plt.title("Figure 3.4: Hold My Hand Screens Navigation Flow (Game Client & Web Portal)", fontsize=12, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r3_fig3_screen_flow.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 4: ERD (Report 3 & Report 4)
# ==========================================
def gen_erd():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    def draw_entity(ax, x, y, w, h, name, fields, bg="#EFF6FF", border="#2563EB"):
        box = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.04", fc=bg, ec=border, lw=1.5, zorder=2)
        ax.add_patch(box)
        # Header
        header_h = 0.5
        hdr = patches.FancyBboxPatch((x, y + h - header_h), w, header_h, boxstyle="round,pad=0.04", fc=border, ec=border, lw=1, zorder=3)
        ax.add_patch(hdr)
        ax.text(x + w/2, y + h - header_h/2, name, ha="center", va="center", fontsize=9, fontweight="bold", color="white", zorder=4)
        # Field list
        fy = y + h - header_h - 0.25
        for f, is_pk, is_fk in fields:
            prefix = "PK " if is_pk else ("FK " if is_fk else "   ")
            fcolor = "#B91C1C" if is_pk else ("#1D4ED8" if is_fk else "#334155")
            fweight = "bold" if (is_pk or is_fk) else "normal"
            ax.text(x + 0.15, fy, prefix + f, fontsize=7.5, fontweight=fweight, color=fcolor, zorder=4)
            fy -= 0.26

    # Entities
    user_fields = [
        ("userId (ObjectId)", True, False),
        ("username (String)", False, False),
        ("email (String)", False, False),
        ("passwordHash (String)", False, False),
        ("role (Enum: Player,Mod,Admin)", False, False),
        ("isBanned (Boolean)", False, False),
        ("createdAt (Date)", False, False),
    ]
    draw_entity(ax, 0.6, 2.4, 2.7, 2.8, "USER", user_fields, bg="#EFF6FF", border="#1E40AF")

    lobby_fields = [
        ("lobbyCode (String, 6-char)", True, False),
        ("player1Id (ObjectId)", False, True),
        ("player2Id (ObjectId)", False, True),
        ("player1Role (Dad | Child)", False, False),
        ("player2Role (Dad | Child)", False, False),
        ("status (WAITING|READY|CLOSED)", False, False),
        ("createdAt (Date)", False, False),
        ("closedBy (ObjectId)", False, True),
    ]
    draw_entity(ax, 4.0, 2.2, 2.8, 3.1, "LOBBY (Redis / Cache)", lobby_fields, bg="#ECFDF5", border="#047857")

    session_fields = [
        ("sessionId (ObjectId)", True, False),
        ("lobbyCode (String)", False, True),
        ("levelId (String: L1, L2, Boss)", False, False),
        ("durationSeconds (Number)", False, False),
        ("result (WIN | LOSE | ABORT)", False, False),
        ("completedAt (Date)", False, False),
    ]
    draw_entity(ax, 7.3, 2.7, 2.5, 2.6, "GAME_SESSION", session_fields, bg="#FEF3C7", border="#B45309")

    audit_fields = [
        ("logId (ObjectId)", True, False),
        ("actorId (ObjectId)", False, True),
        ("action (String: CLOSE_LOBBY...)", False, False),
        ("targetId (String)", False, False),
        ("reason (String)", False, False),
        ("timestamp (Date)", False, False),
    ]
    draw_entity(ax, 2.2, 0.2, 2.8, 1.8, "AUDIT_LOG", audit_fields, bg="#F3E8FF", border="#6D28D9")

    # Relationships
    # User -> Lobby (1:N)
    ax.annotate("", xy=(4.0, 3.8), xytext=(3.3, 3.8), arrowprops=dict(arrowstyle="<->", color="#1E40AF", lw=1.5))
    ax.text(3.65, 4.0, "1 : N", ha="center", fontsize=8, fontweight="bold", color="#1E40AF")

    # Lobby -> GameSession (1:1)
    ax.annotate("", xy=(7.3, 3.8), xytext=(6.8, 3.8), arrowprops=dict(arrowstyle="->", color="#047857", lw=1.5))
    ax.text(7.05, 4.0, "1 : 1", ha="center", fontsize=8, fontweight="bold", color="#047857")

    # User -> AuditLog (1:N)
    ax.annotate("", xy=(2.6, 2.0), xytext=(2.0, 2.4), arrowprops=dict(arrowstyle="->", color="#6D28D9", lw=1.5))
    ax.text(2.1, 2.15, "1 : N", ha="center", fontsize=8, fontweight="bold", color="#6D28D9")

    plt.title("Figure 3.5 / 4.3: Hold My Hand Entity Relationship Diagram (ERD & Document Schema)", fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r3_fig4_erd.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    out_path2 = os.path.join(OUT_DIR, "r4_fig3_db_schema.png")
    plt.savefig(out_path2, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path, "and", out_path2)

# ==========================================
# DIAGRAM 5: System Architecture (Report 4)
# ==========================================
def gen_r4_architecture():
    fig, ax = plt.subplots(figsize=(11, 7), dpi=200)
    ax.set_xlim(0, 11)
    ax.set_ylim(0, 7)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # 3 Layers / Tiers
    # Tier 1: Client Layer
    tier1 = patches.FancyBboxPatch((0.4, 4.4), 10.2, 2.2, boxstyle="round,pad=0.08", fc="#F8FAFC", ec="#3B82F6", lw=1.5, ls="--")
    ax.add_patch(tier1)
    ax.text(0.6, 6.35, "CLIENT TIER (PRESENTATION LAYER)", fontsize=9.5, fontweight="bold", color="#1E3A8A")

    draw_box(ax, 0.7, 4.6, 4.4, 1.6, "Unity 6 Game Client (.exe)",
             "URP 3D Renderer | Input System\nBlocks.Gameplay (Physics & Asymmetric Controllers)\nApiClient.cs (REST) | VoiceChatManager.cs (WSS)\nUnity Lobby & Relay SDK",
             bg="#EFF6FF", border="#2563EB")

    draw_box(ax, 5.6, 4.6, 4.6, 1.6, "Web Portal & Admin Dashboard (ReactJS)",
             "Vite + React 18 + TailwindCSS\nPlayer Profile & Match History Components\nModeration Console (Force Close / Ban)\nAxios HTTP Client + JWT LocalStorage",
             bg="#FEF3C7", border="#D97706")

    # Tier 2: Application / API Layer
    tier2 = patches.FancyBboxPatch((0.4, 2.1), 10.2, 2.0, boxstyle="round,pad=0.08", fc="#F8FAFC", ec="#10B981", lw=1.5, ls="--")
    ax.add_patch(tier2)
    ax.text(0.6, 3.85, "APPLICATION TIER (BACKEND SERVICES ON VERCEL SERVERLESS)", fontsize=9.5, fontweight="bold", color="#065F46")

    draw_box(ax, 0.7, 2.3, 2.9, 1.3, "Auth & User Service",
             "POST /api/auth/login, /register\nJWT Generation (RS256/HS256)\nRole-Based Access Control (RBAC)",
             bg="#DCFCE7", border="#16A34A")

    draw_box(ax, 3.9, 2.3, 3.2, 1.3, "Lobby & Moderation Service",
             "POST /api/game/lobby/* (CRUD, Swap, Ready)\nPOST /api/mod/lobby/:code/close (MOD-001)\nIn-Memory / Redis Fallback Store",
             bg="#DCFCE7", border="#16A34A")

    draw_box(ax, 7.4, 2.3, 2.8, 1.3, "Voice Relay Service (WSS)",
             "WSS /voice (Binary 644-byte frames)\nUnity Token Validation via JWKS\nIn-room Peer Fan-out (No Storage)",
             bg="#DCFCE7", border="#16A34A")

    # Tier 3: Data & Cloud Infrastructure Layer
    tier3 = patches.FancyBboxPatch((0.4, 0.2), 10.2, 1.6, boxstyle="round,pad=0.08", fc="#F8FAFC", ec="#8B5CF6", lw=1.5, ls="--")
    ax.add_patch(tier3)
    ax.text(0.6, 1.55, "DATA & INFRASTRUCTURE TIER", fontsize=9.5, fontweight="bold", color="#5B21B6")

    draw_box(ax, 0.8, 0.4, 2.8, 0.95, "MongoDB Atlas (Cloud)", "Users, GameSessions, AuditLogs", bg="#F3E8FF", border="#7C3AED")
    draw_box(ax, 4.1, 0.4, 2.8, 0.95, "Redis / Upstash (Cache)", "Lobby State Machine TTL 30m", bg="#F3E8FF", border="#7C3AED")
    draw_box(ax, 7.4, 0.4, 2.8, 0.95, "Unity Gaming Services (UGS)", "Unity Lobby & Relay Service, JWKS", bg="#F3E8FF", border="#7C3AED")

    # Connectors between tiers
    draw_arrow(ax, 2.5, 4.6, 2.0, 3.6, "REST API (JWT)", color="#2563EB")
    draw_arrow(ax, 2.9, 4.6, 5.2, 3.6, "Lobby CRUD", color="#2563EB")
    draw_arrow(ax, 4.5, 4.6, 8.5, 3.6, "WSS Audio 16kHz", color="#DC2626")
    draw_arrow(ax, 7.2, 4.6, 5.5, 3.6, "REST / Mod", color="#D97706")

    draw_arrow(ax, 2.0, 2.3, 2.0, 1.35, "Mongoose", color="#7C3AED")
    draw_arrow(ax, 5.5, 2.3, 5.5, 1.35, "ioredis / memory", color="#7C3AED")
    draw_arrow(ax, 8.8, 2.3, 8.8, 1.35, "JWKS / Lobby Sync", color="#7C3AED")

    plt.title("Figure 4.1: Hold My Hand Multi-Tier Distributed Software Architecture", fontsize=12, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig1_system_architecture.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 6: Package Diagram (Report 4)
# ==========================================
def gen_r4_package():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Unity Sub-system Packages
    u_box = patches.FancyBboxPatch((0.5, 0.6), 4.3, 5.0, boxstyle="round,pad=0.08", fc="#EFF6FF", ec="#2563EB", lw=2)
    ax.add_patch(u_box)
    ax.text(2.65, 5.3, "UNITY CLIENT PACKAGES (Asmdef)", ha="center", fontsize=10, fontweight="bold", color="#1E3A8A")

    draw_box(ax, 0.8, 4.1, 3.7, 0.8, "Blocks.Common", "ApiClient, ModApiClient, Common DTOs, Helpers", bg="white", border="#3B82F6")
    draw_box(ax, 0.8, 3.0, 3.7, 0.8, "Blocks.Sessions", "Lobby3DManager, Matchmaking, LobbyScreen UI", bg="white", border="#3B82F6")
    draw_box(ax, 0.8, 1.9, 3.7, 0.8, "Blocks.Gameplay.Core", "VoiceChatManager, VoiceDSPProcessor, CharacterBase", bg="white", border="#3B82F6")
    draw_box(ax, 0.8, 0.8, 1.7, 0.8, "Blocks.Platformer", "Dad/Child Co-op Jump", bg="white", border="#3B82F6")
    draw_box(ax, 2.8, 0.8, 1.7, 0.8, "Blocks.Shooter", "Puzzle Energy Trigger", bg="white", border="#3B82F6")

    # Backend Sub-system Packages
    b_box = patches.FancyBboxPatch((5.2, 0.6), 4.3, 5.0, boxstyle="round,pad=0.08", fc="#ECFDF5", ec="#059669", lw=2)
    ax.add_patch(b_box)
    ax.text(7.35, 5.3, "BACKEND PACKAGES (Node.js/Express)", ha="center", fontsize=10, fontweight="bold", color="#065F46")

    draw_box(ax, 5.5, 4.1, 3.7, 0.8, "api/ & routes/", "auth.js, game.js, mod.js, voice.js (Vercel)", bg="white", border="#10B981")
    draw_box(ax, 5.5, 3.0, 3.7, 0.8, "middleware/", "auth.js (JWT verify), rbac.js (Role guard)", bg="white", border="#10B981")
    draw_box(ax, 5.5, 1.9, 3.7, 0.8, "services/", "voiceRelay.js, jwtService.js, ugsService.js", bg="white", border="#10B981")
    draw_box(ax, 5.5, 0.8, 3.7, 0.8, "store/ & models/", "lobbyStore.js (In-mem/Redis), User.js, Session.js", bg="white", border="#10B981")

    # Dependency arrows
    draw_arrow(ax, 2.65, 3.0, 2.65, 3.8, "references")
    draw_arrow(ax, 2.65, 1.9, 2.65, 2.7, "references")
    draw_arrow(ax, 7.35, 4.1, 7.35, 3.8, "uses")
    draw_arrow(ax, 7.35, 3.0, 7.35, 2.7, "uses")
    draw_arrow(ax, 7.35, 1.9, 7.35, 1.6, "accesses")

    # Cross-subsystem communication
    draw_arrow(ax, 4.5, 4.5, 5.5, 4.5, "HTTP REST / JSON", color="#2563EB", lw=1.5)
    draw_arrow(ax, 4.5, 2.3, 5.5, 2.3, "WSS / Binary PCM", color="#DC2626", lw=1.5)

    plt.title("Figure 4.2: Hold My Hand Software Package Structure Diagram", fontsize=12, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig2_package_diagram.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 7: Sequence Diagram - Login & JWT
# ==========================================
def gen_r4_seq_login():
    fig, ax = plt.subplots(figsize=(10, 5.5), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    lifelines = [
        (1.2, "Player (User)"),
        (3.5, "LoginScreen (UI)"),
        (5.8, "ApiClient.cs"),
        (8.2, "Backend /api/auth"),
        (9.5, "MongoDB"),
    ]

    for x, name in lifelines:
        draw_box(ax, x - 0.7, 4.8, 1.4, 0.5, name, bg="#DBEAFE", border="#1E40AF")
        ax.plot([x, x], [0.5, 4.8], color="#94A3B8", lw=1, ls="--")

    # Sequence steps
    steps = [
        (1.2, 3.5, 4.3, "1: Enter email & password, click Login", "->"),
        (3.5, 5.8, 3.8, "2: PostAsync('/api/auth/login', req)", "->"),
        (5.8, 8.2, 3.3, "3: HTTP POST /api/auth/login", "->"),
        (8.2, 9.5, 2.8, "4: findOne({ email })", "->"),
        (9.5, 8.2, 2.4, "5: User document returned", "-->"),
        (8.2, 8.2, 2.0, "6: Verify bcrypt hash & sign JWT", "self"),
        (8.2, 5.8, 1.6, "7: 200 OK { token, role, userId }", "-->"),
        (5.8, 3.5, 1.2, "8: Save token to PlayerPrefs", "-->"),
        (3.5, 1.2, 0.8, "9: Transition to LobbyBrowser Screen", "-->"),
    ]

    for x1, x2, y, label, style in steps:
        if style == "self":
            # Self call
            ax.annotate("", xy=(x1, y - 0.2), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="->", color="#1E40AF", lw=1.2,
                                        connectionstyle="arc3,rad=-0.4"))
            ax.text(x1 + 0.1, y - 0.1, label, fontsize=7.5, fontweight="semibold", color="#0F172A")
        else:
            ls = "--" if style == "-->" else "-"
            ax.annotate("", xy=(x2, y), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="->", color="#1E40AF", lw=1.2, ls=ls))
            mx = (x1 + x2)/2
            ax.text(mx, y + 0.08, label, ha="center", fontsize=7.5, fontweight="semibold", color="#0F172A",
                    bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="none", alpha=0.9))

    plt.title("Figure 4.5: Sequence Diagram for User Login and JWT Issuance", fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig5_seq_login.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 8: Sequence Diagram - Voice Chat (VOICE-001–004)
# ==========================================
def gen_r4_seq_voice():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    lifelines = [
        (1.0, "Player A (Dad)"),
        (3.2, "VoiceChatMgr A"),
        (5.5, "WSS /voice (Vercel)"),
        (7.8, "VoiceChatMgr B"),
        (9.5, "Player B (Child)"),
    ]

    for x, name in lifelines:
        draw_box(ax, x - 0.8, 5.3, 1.6, 0.5, name, bg="#DCFCE7", border="#16A34A")
        ax.plot([x, x], [0.4, 5.3], color="#94A3B8", lw=1, ls="--")

    steps = [
        (3.2, 5.5, 4.8, "1: Connect WSS + send auth frame (Unity Token)", "->"),
        (5.5, 3.2, 4.3, "2: 200 OK: {type:'ready', playerId, lobbyCode}", "-->"),
        (7.8, 5.5, 3.9, "3: Client B connects & authenticates", "->"),
        (5.5, 7.8, 3.5, "4: {type:'ready'} sent to Client B", "-->"),
        (1.0, 3.2, 3.0, "5: Dad presses key 'V' (PTT Active)", "->"),
        (3.2, 3.2, 2.6, "6: ShiftPitch(-4 semitones) via VoiceDSP", "self"),
        (3.2, 5.5, 2.1, "7: Binary frame (644B: 4B seq + 320 PCM samples)", "->"),
        (5.5, 7.8, 1.6, "8: Forward binary frame to peer (Client B)", "->"),
        (7.8, 9.5, 1.1, "9: JitterBuffer -> AudioSource.PlayOneShot()", "->"),
        (1.0, 3.2, 0.6, "10: Release key 'V' -> Stop streaming", "->"),
    ]

    for x1, x2, y, label, style in steps:
        if style == "self":
            ax.annotate("", xy=(x1, y - 0.2), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.2,
                                        connectionstyle="arc3,rad=-0.4"))
            ax.text(x1 + 0.1, y - 0.1, label, fontsize=7.5, fontweight="semibold", color="#0F172A")
        else:
            ls = "--" if style == "-->" else "-"
            ax.annotate("", xy=(x2, y), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="->", color="#16A34A", lw=1.2, ls=ls))
            mx = (x1 + x2)/2
            ax.text(mx, y + 0.08, label, ha="center", fontsize=7.5, fontweight="semibold", color="#0F172A",
                    bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="none", alpha=0.9))

    plt.title("Figure 4.8: Sequence Diagram for Voice Chat Handshake & Binary Audio Streaming (VOICE-001–004)", fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig8_seq_voice.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 9: Sequence Diagram - Force Close (MOD-001)
# ==========================================
def gen_r4_seq_mod_close():
    fig, ax = plt.subplots(figsize=(10, 5.5), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    lifelines = [
        (1.2, "Moderator (Web)"),
        (3.8, "Backend /api/mod"),
        (6.4, "LobbyStore (Redis/RAM)"),
        (8.8, "Unity Game Clients"),
    ]

    for x, name in lifelines:
        draw_box(ax, x - 0.9, 4.8, 1.8, 0.5, name, bg="#FFE4E6", border="#E11D48")
        ax.plot([x, x], [0.5, 4.8], color="#94A3B8", lw=1, ls="--")

    steps = [
        (1.2, 3.8, 4.2, "1: POST /api/mod/lobby/:code/close {reason, notifyPlayers}", "->"),
        (3.8, 3.8, 3.7, "2: Verify JWT & role == 'Moderator'", "self"),
        (3.8, 6.4, 3.2, "3: getLobby(code) -> check status != 'CLOSED'", "->"),
        (6.4, 3.8, 2.7, "4: Return current lobby data", "-->"),
        (3.8, 6.4, 2.2, "5: update(code, {status:'CLOSED', closedBy, reason})", "->"),
        (3.8, 1.2, 1.6, "6: 200 OK {success:true, currentStatus:'CLOSED'}", "-->"),
        (3.8, 8.8, 1.0, "7: [BLOCKED] Push force close event to Unity clients", "-->"),
    ]

    for x1, x2, y, label, style in steps:
        color = "#DC2626" if "BLOCKED" in label else "#1E40AF"
        if style == "self":
            ax.annotate("", xy=(x1, y - 0.2), xytext=(x1, y),
                        arrowprops=dict(arrowstyle="->", color=color, lw=1.2, connectionstyle="arc3,rad=-0.4"))
            ax.text(x1 + 0.1, y - 0.1, label, fontsize=7.5, fontweight="semibold", color="#0F172A")
        else:
            ls = "--" if style == "-->" else "-"
            ax.annotate("", xy=(x2, y), xytext=(x1, y), arrowprops=dict(arrowstyle="->", color=color, lw=1.2, ls=ls))
            mx = (x1 + x2)/2
            ax.text(mx, y + 0.08, label, ha="center", fontsize=7.5, fontweight="semibold", color=color,
                    bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="none", alpha=0.9))

    plt.title("Figure 4.9: Sequence Diagram for Moderator Force Close Lobby (MOD-001)", fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig9_seq_mod_close.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 10: Test Pipeline & Results (Report 5)
# ==========================================
def gen_r5_charts():
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(10, 4.5), dpi=200)
    fig.patch.set_facecolor("white")

    # Pie chart of results
    labels = ["PASS (20 TCs)", "BLOCKED (1 TC)"]
    sizes = [20, 1]
    colors = ["#10B981", "#EF4444"]
    explode = (0, 0.1)

    ax1.pie(sizes, explode=explode, labels=labels, colors=colors, autopct='%1.1f%%',
            startangle=140, textprops={'fontsize': 9, 'fontweight': 'bold'})
    ax1.set_title("Overall Test Execution (21 Test Cases)\n95.2% Pass Rate", fontsize=10, fontweight="bold", color="#0F172A")

    # Bar chart by feature
    categories = ["Backend API", "Voice Chat", "Voice E2E", "Unity Core"]
    pass_counts = [10, 6, 0, 4]
    blocked_counts = [0, 0, 1, 0]

    x = range(len(categories))
    width = 0.4
    ax2.bar([i - width/2 for i in x], pass_counts, width, label="Passed", color="#10B981")
    ax2.bar([i + width/2 for i in x], blocked_counts, width, label="Blocked", color="#EF4444")

    ax2.set_ylabel("Number of Test Cases", fontsize=9, fontweight="bold")
    ax2.set_title("Test Execution Breakdown by Module", fontsize=10, fontweight="bold", color="#0F172A")
    ax2.set_xticks(list(x))
    ax2.set_xticklabels(categories, fontsize=8.5)
    ax2.legend(fontsize=8.5)
    ax2.grid(axis='y', linestyle='--', alpha=0.5)

    plt.suptitle("Figure 5.1: Test Execution Results and Pass Rate Breakdown (Report 5)", fontsize=11, fontweight="bold", y=1.02, color="#0F172A")
    plt.tight_layout()
    out_path = os.path.join(OUT_DIR, "r5_fig1_test_results.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

if __name__ == "__main__":
    print("Generating all diagrams...")
    gen_r3_context()
    gen_r3_usecases()
    gen_r3_screen_flow()
    gen_erd()
    gen_r4_architecture()
    gen_r4_package()
    gen_r4_seq_login()
    gen_r4_seq_voice()
    gen_r4_seq_mod_close()
    gen_r5_charts()
    print("All diagrams generated successfully!")


# ==========================================
# DIAGRAM 11: Class Diagram - Lobby (Report 4)
# ==========================================
def gen_r4_class_lobby():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    def draw_class(ax, x, y, w, h, name, attrs, methods, bg="#EFF6FF", border="#2563EB"):
        box = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.03", fc=bg, ec=border, lw=1.5, zorder=2)
        ax.add_patch(box)
        # Header
        hdr_h = 0.45
        hdr = patches.Rectangle((x, y + h - hdr_h), w, hdr_h, fc=border, ec=border, zorder=3)
        ax.add_patch(hdr)
        ax.text(x + w/2, y + h - hdr_h/2, name, ha="center", va="center", fontsize=8.5, fontweight="bold", color="white", zorder=4)
        
        # Attrs
        ay = y + h - hdr_h - 0.2
        for a in attrs:
            ax.text(x + 0.1, ay, "- " + a, fontsize=7, color="#1E293B", zorder=4)
            ay -= 0.2
        
        # Separator line
        sep_y = ay - 0.05
        ax.plot([x, x + w], [sep_y, sep_y], color=border, lw=0.8, zorder=3)
        
        # Methods
        my = sep_y - 0.2
        for m in methods:
            ax.text(x + 0.1, my, "+ " + m, fontsize=7, color="#0F172A", fontweight="semibold", zorder=4)
            my -= 0.2

    # ApiClient
    draw_class(ax, 0.5, 3.2, 3.8, 2.5, "ApiClient (Static)",
               ["_baseUrl: string", "_authToken: string", "_isInitialized: bool"],
               ["SetAuthToken(token: string): void",
                "GetAsync<T>(path: string): Task<T>",
                "PostAsync<T>(path: string, body: object): Task<T>",
                "DeleteAsync<T>(path: string): Task<T>"],
               bg="#EFF6FF", border="#1D4ED8")

    # ModApiClient (inherits ApiClient)
    draw_class(ax, 0.5, 0.5, 3.8, 2.2, "ModApiClient : ApiClient",
               ["_modRole: string = 'Moderator'"],
               ["CloseLobbyAsync(code: string, req: CloseReq): Task<CloseResp>",
                "AdjustPlayerStatsAsync(code: string, stats: DTO): Task<bool>",
                "KickPlayerAsync(code: string, playerId: string): Task<bool>"],
               bg="#FFE4E6", border="#BE123C")

    # Lobby3DManager
    draw_class(ax, 5.2, 2.8, 4.3, 2.9, "Lobby3DManager (MonoBehaviour)",
               ["_currentLobby: LobbyDTO", "_isDadRole: bool", "_isReady: bool",
                "_micButton: Button", "_settingsPopup: VisualElement"],
               ["InitializeUI(): void",
                "OnRoleSwapClicked(): Task<void>",
                "OnReadyClicked(): Task<void>",
                "OnMicToggleClicked(): void",
                "UpdateLobbyPlayerDisplay(lobby: LobbyDTO): void"],
               bg="#DCFCE7", border="#15803D")

    # DTOs
    draw_class(ax, 5.2, 0.5, 4.3, 1.9, "LobbyDTO & PlayerSlot (DTOs)",
               ["lobbyCode: string", "status: string", "player1: PlayerSlot", "player2: PlayerSlot"],
               ["playerId: string", "username: string", "role: string ('Dad'|'Child')", "isReady: bool"],
               bg="#FEF3C7", border="#B45309")

    # Inheritance arrow ModApiClient -> ApiClient
    ax.annotate("", xy=(2.4, 3.2), xytext=(2.4, 2.7),
                arrowprops=dict(arrowstyle="->", color="#1D4ED8", lw=1.5, ls="--"))
    ax.text(2.5, 2.9, "<<extends>>", fontsize=7.5, color="#1D4ED8")

    # Association Lobby3DManager -> ApiClient
    ax.annotate("", xy=(4.3, 4.5), xytext=(5.2, 4.5),
                arrowprops=dict(arrowstyle="->", color="#15803D", lw=1.2))
    ax.text(4.75, 4.65, "calls", fontsize=7.5, color="#15803D")

    plt.title("Figure 4.4: Class Diagram for Account & Lobby Management (Blocks.Common & Blocks.Sessions)",
              fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig4_class_lobby.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 12: Sequence Diagram - Create & Join Lobby
# ==========================================
def gen_r4_seq_lobby():
    fig, ax = plt.subplots(figsize=(10, 5.5), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 5.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    lifelines = [
        (1.0, "Player 1 (Host)"),
        (3.2, "Player 2 (Peer)"),
        (5.5, "Backend API"),
        (7.8, "LobbyStore (Redis/RAM)"),
        (9.4, "Unity UGS Lobby"),
    ]

    for x, name in lifelines:
        draw_box(ax, x - 0.8, 4.8, 1.6, 0.5, name, bg="#EFF6FF", border="#2563EB")
        ax.plot([x, x], [0.4, 4.8], color="#94A3B8", lw=1, ls="--")

    steps = [
        (1.0, 5.5, 4.3, "1: POST /api/game/lobby/create (Auth Token)", "->"),
        (5.5, 7.8, 3.8, "2: createLobby(p1Id, 'Dad') -> code 'AB12CD'", "->"),
        (5.5, 1.0, 3.4, "3: 200 OK { lobbyCode: 'AB12CD', role: 'Dad' }", "-->"),
        (1.0, 3.2, 2.9, "4: Share code 'AB12CD' with friend (Discord)", "-->"),
        (3.2, 5.5, 2.4, "5: POST /api/game/lobby/join { lobbyCode: 'AB12CD' }", "->"),
        (5.5, 7.8, 1.9, "6: assignSlot(p2Id, 'Child') -> status 'WAITING'", "->"),
        (5.5, 3.2, 1.5, "7: 200 OK { lobbyCode, role: 'Child' }", "-->"),
        (1.0, 5.5, 1.0, "8: POST /api/game/lobby/:code/ready", "->"),
        (3.2, 5.5, 0.6, "9: POST /api/game/lobby/:code/ready -> Status 'READY'", "->"),
    ]

    for x1, x2, y, label, style in steps:
        ls = "--" if style == "-->" else "-"
        ax.annotate("", xy=(x2, y), xytext=(x1, y),
                    arrowprops=dict(arrowstyle="->", color="#2563EB", lw=1.2, ls=ls))
        mx = (x1 + x2)/2
        ax.text(mx, y + 0.08, label, ha="center", fontsize=7.5, fontweight="semibold", color="#0F172A",
                bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="none", alpha=0.9))

    plt.title("Figure 4.6: Sequence Diagram for 2-Player Matchmaking and Role Assignment", fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig6_seq_lobby.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 13: Class Diagram - Voice Chat (Report 4)
# ==========================================
def gen_r4_class_voice():
    fig, ax = plt.subplots(figsize=(10, 6), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    def draw_class(ax, x, y, w, h, name, attrs, methods, bg="#DCFCE7", border="#16A34A"):
        box = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.03", fc=bg, ec=border, lw=1.5, zorder=2)
        ax.add_patch(box)
        hdr_h = 0.45
        hdr = patches.Rectangle((x, y + h - hdr_h), w, hdr_h, fc=border, ec=border, zorder=3)
        ax.add_patch(hdr)
        ax.text(x + w/2, y + h - hdr_h/2, name, ha="center", va="center", fontsize=8.5, fontweight="bold", color="white", zorder=4)
        
        ay = y + h - hdr_h - 0.2
        for a in attrs:
            ax.text(x + 0.1, ay, "- " + a, fontsize=7, color="#1E293B", zorder=4)
            ay -= 0.18
        
        sep_y = ay - 0.05
        ax.plot([x, x + w], [sep_y, sep_y], color=border, lw=0.8, zorder=3)
        
        my = sep_y - 0.2
        for m in methods:
            ax.text(x + 0.1, my, "+ " + m, fontsize=7, color="#0F172A", fontweight="semibold", zorder=4)
            my -= 0.18

    # VoiceChatManager
    draw_class(ax, 0.5, 1.2, 4.4, 4.2, "VoiceChatManager (MonoBehaviour)",
               ["_ws: ClientWebSocket", "_micClip: AudioClip",
                "_jitterBuffer: Queue<byte[]>", "_isPTT: bool = true",
                "_pttKey: KeyCode = KeyCode.V", "_isAutoGate: bool",
                "_sampleRate: int = 16000", "_frameSamples: int = 320",
                "_playerRole: PlayerRole (Dad | Child)"],
               ["ConnectAsync(url, lobbyId, token): Task<void>",
                "StartMicrophone(): void",
                "StopMicrophone(): void",
                "SendAudioFrame(pcmSamples: short[]): Task<void>",
                "ReceiveLoop(): Task<void>",
                "ProcessJitterBuffer(): void",
                "SetMute(isMuted: bool): void"],
               bg="#DCFCE7", border="#15803D")

    # VoiceDSPProcessor
    draw_class(ax, 5.4, 3.2, 4.2, 2.2, "VoiceDSPProcessor (Static DSP)",
               ["DAD_SEMITONES: float = -4.0f",
                "CHILD_SEMITONES: float = +5.0f",
                "SAMPLE_RATE: int = 16000"],
               ["ProcessVoice(samples: float[], role: Role): float[]",
                "ShiftPitch(samples: float[], semitones: float): float[]",
                "ApplyFormantEnvelope(samples: float[], factor: float): float[]"],
               bg="#FEF3C7", border="#B45309")

    # VoiceRelayService (Backend)
    draw_class(ax, 5.4, 0.6, 4.2, 2.1, "voiceRelay.js (Node.js WebSocket Server)",
               ["rooms: Map<lobbyId, Set<WebSocket>>",
                "UNITY_JWKS_URL: string",
                "FRAME_SIZE: int = 644 (4B seq + 640B PCM)"],
               ["attachVoiceRelay(server): void",
                "handleAuthFrame(ws, jsonFrame): Promise<bool>",
                "relayBinaryFrame(ws, lobbyId, buffer): void"],
               bg="#EFF6FF", border="#1D4ED8")

    # Associations
    draw_arrow(ax, 4.9, 4.2, 5.4, 4.2, "applies filter", color="#15803D")
    draw_arrow(ax, 4.9, 1.6, 5.4, 1.6, "streams WSS", color="#1D4ED8")

    plt.title("Figure 4.7: Class Diagram for Voice Chat Streaming & DSP Shifter (VOICE-001–004)",
              fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r4_fig7_class_voice.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

# ==========================================
# DIAGRAM 14: Test Strategy Pipeline (Report 5)
# ==========================================
def gen_r5_pipeline():
    fig, ax = plt.subplots(figsize=(10, 4.5), dpi=200)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 4.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    draw_box(ax, 0.4, 1.5, 2.0, 1.8, "Level 1: Unit Test",
             "Jest & Supertest (Backend)\nVoiceDSP Freq Tests (178/303Hz)\nUnity Component Tests\nCoverage: 100% (16/16 Pass)",
             bg="#DCFCE7", border="#16A34A")

    draw_box(ax, 2.8, 1.5, 2.0, 1.8, "Level 2: Integration",
             "Auth -> Lobby CRUD Chain\nVoice WS Auth Handshake\nMod Force Close API\nCoverage: 100% (4/4 Pass)",
             bg="#DBEAFE", border="#2563EB")

    draw_box(ax, 5.2, 1.5, 2.0, 1.8, "Level 3: System Test",
             "Unity Game Client + Backend\nLobby 3D UI & Ready Sync\nVOICE-005 (2-client live audio)\nStatus: BLOCKED (needs 2 PCs)",
             bg="#FEE2E2", border="#DC2626")

    draw_box(ax, 7.6, 1.5, 2.0, 1.8, "Level 4: Acceptance",
             "Capstone Review Deliverables\n2-Player Asymmetric Puzzle\nLatency < 150ms / Target 60 FPS\nStatus: PLANNED (Sprint 3)",
             bg="#FEF3C7", border="#D97706")

    draw_arrow(ax, 2.4, 2.4, 2.8, 2.4, "Merge develop")
    draw_arrow(ax, 4.8, 2.4, 5.2, 2.4, "Deploy Vercel")
    draw_arrow(ax, 7.2, 2.4, 7.6, 2.4, "Release Demo")

    # Bottom status bar
    status_box = patches.FancyBboxPatch((0.4, 0.3), 9.2, 0.8, boxstyle="round,pad=0.03", fc="#F8FAFC", ec="#64748B", lw=1)
    ax.add_patch(status_box)
    ax.text(5.0, 0.7, "Overall Testing Progress: 20 / 21 Test Cases PASSED (95.2% Success Rate)",
            ha="center", fontsize=9.5, fontweight="bold", color="#10B981")
    ax.text(5.0, 0.45, "Pending Blocker: VOICE-005 (Requires dual-machine live audio test environment)",
            ha="center", fontsize=8, color="#DC2626")

    plt.title("Figure 5.2: Multi-Level Test Strategy and Verification Pipeline",
              fontsize=11, fontweight="bold", pad=12, color="#0F172A")
    out_path = os.path.join(OUT_DIR, "r5_fig2_test_pipeline.png")
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("Generated", out_path)

if __name__ == "__main__":
    gen_r4_class_lobby()
    gen_r4_seq_lobby()
    gen_r4_class_voice()
    gen_r5_pipeline()
