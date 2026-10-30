# -*- coding: utf-8 -*-
"""
Generate system architecture diagram with:
1. NO overlap between Title and Legend (ample horizontal space).
2. Clean, orthogonal, parallel bus connections between Client Side -> NGINX -> API Gateway -> Services (NO criss-crossing diagonal lines).
3. Internal gRPC / Auth lines route cleanly AROUND the services in a dedicated left corridor, with 100% HORIZONTAL text (easy to read, NO vertical/rotated text).
4. Balanced, spacious typography and professional enterprise architecture styling matching the user's reference diagram.
"""
import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)
OUT_PATH = os.path.join(OUT_DIR, "system_architecture_detailed.png")

fig, ax = plt.subplots(figsize=(16.5, 9.5), dpi=200)
ax.set_xlim(0, 16.5)
ax.set_ylim(0, 9.5)
ax.axis("off")
fig.patch.set_facecolor("white")

# =============================================================================
# 1. HEADER: TITLE & LEGEND (SEPARATED WITH AMPLE SPACE - NO OVERLAP)
# =============================================================================
# Title at top-left
ax.text(0.6, 9.05, "1. System Design", fontsize=16, fontweight="bold", color="#0F172A", family="DejaVu Sans")
ax.text(0.6, 8.65, "1.1 System Architecture", fontsize=13.5, fontweight="bold", color="#1E293B", family="DejaVu Sans")

# Legend box at (x=4.0 to 10.5, y=8.55 to 9.25) - COMPLETELY SEPARATE from Title
leg_bg = patches.FancyBboxPatch((4.0, 8.5), 6.5, 0.75, boxstyle="round,pad=0.04",
                               fc="#F8FAFC", ec="#E2E8F0", lw=1.0, zorder=2)
ax.add_patch(leg_bg)

# Legend item 1: Http Request (Solid black)
ax.annotate("", xy=(5.2, 9.0), xytext=(4.3, 9.0),
            arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.4))
ax.text(4.75, 9.08, "Http Request", ha="center", va="bottom", fontsize=8, color="#0F172A", fontweight="semibold")

# Legend item 2: Http Response (Dashed grey)
ax.annotate("", xy=(4.3, 8.68), xytext=(5.2, 8.68),
            arrowprops=dict(arrowstyle="->", color="#64748B", lw=1.3, ls="--"))
ax.text(4.75, 8.6, "Http Response", ha="center", va="top", fontsize=8, color="#475569")

# Legend item 3: WSS Audio Relay (Solid Red)
ax.annotate("", xy=(7.2, 9.0), xytext=(6.3, 9.0),
            arrowprops=dict(arrowstyle="->", color="#DC2626", lw=1.6))
ax.text(6.75, 9.08, "WSS Audio Relay", ha="center", va="bottom", fontsize=8, color="#DC2626", fontweight="bold")

# Legend item 4: Internal Call / gRPC (Dotted Blue)
ax.annotate("", xy=(7.2, 8.68), xytext=(6.3, 8.68),
            arrowprops=dict(arrowstyle="->", color="#2563EB", lw=1.4, ls=":"))
ax.text(6.75, 8.6, "gRPC / Internal", ha="center", va="top", fontsize=8, color="#2563EB", fontweight="semibold")

# Legend item 5: Pub/Sub Event (Orange)
ax.annotate("", xy=(9.2, 8.84), xytext=(8.3, 8.84),
            arrowprops=dict(arrowstyle="<->", color="#EA580C", lw=1.4))
ax.text(8.75, 8.92, "Publish / Subscribe", ha="center", va="bottom", fontsize=8, color="#EA580C", fontweight="semibold")


# =============================================================================
# HELPER DRAWING FUNCTIONS
# =============================================================================
def draw_dashed_box(x, y, w, h, label="", label_pos="top_right", bg="#FFFFFF", border="#94A3B8"):
    box = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.08",
                                 fc=bg, ec=border, lw=1.2, ls="--", zorder=1)
    ax.add_patch(box)
    if label:
        if label_pos == "top_right":
            ax.text(x + w - 0.2, y + h - 0.22, label, ha="right", va="top",
                    fontsize=8.5, fontweight="bold", color="#64748B")
        elif label_pos == "center_top":
            ax.text(x + w/2, y + h - 0.22, label, ha="center", va="top",
                    fontsize=9, fontweight="bold", color="#334155")
    return box

def draw_service_cube(x, y, size=0.22, color="#3B82F6"):
    dx = size * 0.866
    dy = size * 0.5
    top = patches.Polygon([[x, y + size], [x + dx, y + dy], [x, y], [x - dx, y + dy]],
                          closed=True, fc=color, ec="#1E3A8A", lw=0.9, zorder=5)
    left = patches.Polygon([[x - dx, y + dy], [x, y], [x, y - size], [x - dx, y - dy]],
                           closed=True, fc="#1D4ED8", ec="#1E3A8A", lw=0.9, zorder=5)
    right = patches.Polygon([[x + dx, y + dy], [x, y], [x, y - size], [x + dx, y - dy]],
                            closed=True, fc="#60A5FA", ec="#1E3A8A", lw=0.9, zorder=5)
    ax.add_patch(top)
    ax.add_patch(left)
    ax.add_patch(right)

def draw_db_cylinder(x, y, w=0.36, h=0.46, color="#0EA5E9"):
    body = patches.Rectangle((x - w/2, y - h/2), w, h, fc=color, ec="#0369A1", lw=0.9, zorder=4)
    ax.add_patch(body)
    bot_ell = patches.Ellipse((x, y - h/2), w, w*0.38, fc=color, ec="#0369A1", lw=0.9, zorder=4)
    ax.add_patch(bot_ell)
    mid_ell = patches.Ellipse((x, y), w, w*0.38, fc=color, ec="#0284C7", lw=0.7, zorder=4)
    ax.add_patch(mid_ell)
    top_ell = patches.Ellipse((x, y + h/2), w, w*0.38, fc="#38BDF8", ec="#0369A1", lw=0.9, zorder=5)
    ax.add_patch(top_ell)

def draw_client_card(x, y, title, subtitle, icon_type="pc", border="#2563EB", bg="#F8FAFC"):
    card = patches.FancyBboxPatch((x, y), 1.7, 1.25, boxstyle="round,pad=0.04",
                                  fc=bg, ec=border, lw=1.2, zorder=3)
    ax.add_patch(card)
    cx = x + 0.85
    cy = y + 0.8
    if icon_type == "pc":
        screen = patches.FancyBboxPatch((cx - 0.22, cy - 0.1), 0.44, 0.32,
                                        boxstyle="round,pad=0.02", fc="#1E293B", ec="#0F172A", lw=1.0, zorder=4)
        ax.add_patch(screen)
        inner = patches.Rectangle((cx - 0.18, cy - 0.06), 0.36, 0.24, fc="#38BDF8", ec="none", zorder=5)
        ax.add_patch(inner)
        stand = patches.Polygon([[cx - 0.05, cy - 0.1], [cx + 0.05, cy - 0.1],
                                 [cx + 0.08, cy - 0.18], [cx - 0.08, cy - 0.18]],
                                closed=True, fc="#64748B", ec="#0F172A", lw=0.7, zorder=4)
        ax.add_patch(stand)
    elif icon_type == "web":
        win = patches.FancyBboxPatch((cx - 0.22, cy - 0.18), 0.44, 0.36,
                                     boxstyle="round,pad=0.02", fc="white", ec="#2563EB", lw=1.0, zorder=4)
        ax.add_patch(win)
        bar = patches.Rectangle((cx - 0.22, cy + 0.09), 0.44, 0.09, fc="#2563EB", ec="none", zorder=5)
        ax.add_patch(bar)
        ax.plot([cx - 0.16, cx - 0.11, cx - 0.06], [cy + 0.135, cy + 0.135, cy + 0.135], 'o', color="white", ms=2.0, zorder=6)

    ax.text(cx, y + 0.36, title, ha="center", va="center", fontsize=7.8, fontweight="bold", color="#0F172A", zorder=4)
    ax.text(cx, y + 0.16, subtitle, ha="center", va="center", fontsize=6.8, color="#475569", zorder=4)


# =============================================================================
# 2. CLIENT SIDE (Left Box)
# =============================================================================
draw_dashed_box(0.5, 1.0, 2.1, 7.2, label="Client Side", label_pos="center_top", bg="#FAFAFA", border="#94A3B8")

draw_client_card(0.7, 6.4, "Unity Client (Dad)", "Win64 PC • URP 3D", icon_type="pc", border="#1E40AF", bg="#EFF6FF")
draw_client_card(0.7, 4.4, "Unity Client (Child)", "Win64 PC • URP 3D", icon_type="pc", border="#1E40AF", bg="#EFF6FF")
draw_client_card(0.7, 1.8, "Web Portal", "ReactJS 18 + Vite", icon_type="web", border="#047857", bg="#F0FDF4")

# Internal Client Bus (Clean right vertical collector bus)
ax.plot([2.4, 2.7, 2.7], [7.0, 7.0, 5.0], color="#94A3B8", lw=1.2, ls=":", zorder=2)
ax.plot([2.4, 2.7], [5.0, 5.0], color="#94A3B8", lw=1.2, ls=":", zorder=2)
ax.plot([2.4, 2.7, 2.7], [2.4, 2.4, 5.0], color="#94A3B8", lw=1.2, ls=":", zorder=2)


# =============================================================================
# 3. CONNECTION: CLIENT SIDE -> NGINX (CLEAN PARALLEL HORIZONTAL LINES)
# =============================================================================
# Line 1 (Upper, y=5.35): HTTP Request / Response
ax.annotate("", xy=(3.4, 5.35), xytext=(2.7, 5.35),
            arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.3))
ax.annotate("", xy=(2.7, 5.15), xytext=(3.4, 5.15),
            arrowprops=dict(arrowstyle="->", color="#64748B", lw=1.3, ls="--"))

# Line 2 (Lower, y=4.65): WSS Audio Relay (Bidirectional)
ax.annotate("", xy=(3.4, 4.65), xytext=(2.7, 4.65),
            arrowprops=dict(arrowstyle="<->", color="#DC2626", lw=1.5))
ax.text(3.05, 4.8, "WSS /voice", ha="center", va="bottom", fontsize=6.8, color="#DC2626", fontweight="bold")


# =============================================================================
# 4. NGINX / VERCEL EDGE REVERSE PROXY
# =============================================================================
nginx_box = patches.FancyBboxPatch((3.4, 3.8), 1.25, 2.1, boxstyle="round,pad=0.06",
                                  fc="#047857", ec="#065F46", lw=1.5, zorder=3)
ax.add_patch(nginx_box)
ax.text(4.025, 5.45, "NGINX", ha="center", va="center", fontsize=11, fontweight="bold", color="white", zorder=4)
ax.text(4.025, 5.1, "/ Vercel Edge", ha="center", va="center", fontsize=7.8, color="#A7F3D0", zorder=4)
ax.text(4.025, 4.5, "Reverse Proxy\nSSL Termination\nPath Routing\n(/api/* & /voice)",
        ha="center", va="center", fontsize=6.8, color="white", zorder=4)


# =============================================================================
# 5. CONNECTION: NGINX -> API GATEWAY (CLEAN PARALLEL HORIZONTAL LINES)
# =============================================================================
ax.annotate("", xy=(5.2, 5.35), xytext=(4.65, 5.35),
            arrowprops=dict(arrowstyle="->", color="#0F172A", lw=1.3))
ax.annotate("", xy=(4.65, 5.15), xytext=(5.2, 5.15),
            arrowprops=dict(arrowstyle="->", color="#64748B", lw=1.3, ls="--"))

# WSS line bypass directly to Voice Relay Service through API Gateway
ax.annotate("", xy=(5.2, 4.65), xytext=(4.65, 4.65),
            arrowprops=dict(arrowstyle="<->", color="#DC2626", lw=1.5))


# =============================================================================
# 6. API GATEWAY (Auth, Routing & RBAC)
# =============================================================================
draw_dashed_box(5.2, 3.5, 1.25, 2.7, label="API Gateway", label_pos="center_top", bg="#FFFBEB", border="#F59E0B")
gw_icon = patches.FancyBboxPatch((5.45, 4.55), 0.75, 0.85, boxstyle="round,pad=0.04", fc="#F59E0B", ec="#D97706", lw=1.2, zorder=3)
ax.add_patch(gw_icon)
ax.text(5.825, 5.15, "GATEWAY", ha="center", va="center", fontsize=8, fontweight="bold", color="white", zorder=4)
ax.text(5.825, 4.8, "JWT Verify\nRBAC Guard", ha="center", va="center", fontsize=6.8, color="#78350F", zorder=4)
ax.text(5.825, 3.85, "Auth & Routing\nRate Limiter", ha="center", va="center", fontsize=6.8, color="#92400E", zorder=4)


# =============================================================================
# 7. SERVER SIDE (Large Container Box)
# =============================================================================
draw_dashed_box(6.8, 1.0, 6.7, 7.2, label="Server Side", label_pos="top_right", bg="#FAFAFA", border="#64748B")

# 7.1 Inner Core Services Box
draw_dashed_box(7.0, 1.25, 3.8, 6.6, label="", bg="#FFFFFF", border="#CBD5E1")
ax.text(8.9, 7.6, "Core Microservices & Data Layer", ha="center", va="center", fontsize=9, fontweight="bold", color="#1E293B")

# 6 Microservices definitions: (Title, DB_name, y_center, service_color, db_color)
services_data = [
    ("Identity & Auth Service", "Users DB\n(MongoDB)", 6.9, "#3B82F6", "#0284C7"),
    ("Lobby & Matchmaking", "Lobby Cache\n(Redis/RAM)", 5.9, "#10B981", "#059669"),
    ("Voice Relay Service (WSS)", "Room Sockets\n(In-Memory)", 4.9, "#EF4444", "#DC2626"),
    ("Moderation Service (MOD-001)", "Audit Logs DB\n(MongoDB)", 3.9, "#8B5CF6", "#7C3AED"),
    ("Game Session & ELO Service", "Sessions DB\n(MongoDB)", 2.9, "#F59E0B", "#D97706"),
    ("Companion Bot AI Service", "Bot Config DB\n(GameDB)", 1.9, "#EC4899", "#DB2777"),
]

# Clean Horizontal Distribution Rail from Gateway (x=6.45) to each Service (x=7.45)
ax.plot([6.45, 6.75], [5.0, 5.0], color="#0F172A", lw=1.3, zorder=2)
ax.plot([6.75, 6.75], [1.9, 6.9], color="#64748B", lw=1.3, ls="-", zorder=2)

for s_name, db_name, y_pos, s_col, db_col in services_data:
    is_voice = ("Voice" in s_name)
    branch_color = "#DC2626" if is_voice else "#0F172A"
    branch_lw = 1.5 if is_voice else 1.2
    ax.annotate("", xy=(7.4, y_pos), xytext=(6.75, y_pos),
                arrowprops=dict(arrowstyle="->", color=branch_color, lw=branch_lw))

    # Service Cube
    draw_service_cube(7.7, y_pos, size=0.2, color=s_col)
    ax.text(7.7, y_pos - 0.28, s_name, ha="center", va="top", fontsize=6.8, fontweight="bold", color="#0F172A")

    # DB Cylinder
    draw_db_cylinder(10.15, y_pos, w=0.34, h=0.44, color=db_col)
    ax.text(10.15, y_pos - 0.28, db_name, ha="center", va="top", fontsize=6.5, color="#334155")

    # Internal bidirectional link between service & DB
    ax.annotate("", xy=(9.88, y_pos), xytext=(8.32, y_pos),
                arrowprops=dict(arrowstyle="<->", color="#64748B", lw=1.1))


# =============================================================================
# 8. INTERNAL ORTHOGONAL SERVICE CALLS (ROUTING IN DEDICATED LEFT CORRIDOR)
# NO OVERLAP, NO ROTATED TEXT - TEXT IS 100% HORIZONTAL AND EASY TO READ!
# =============================================================================
# Corridor 1: Moderation Service (y=3.9) -> Identity & Auth (y=6.9) for Token Verification
ax.plot([7.4, 7.08], [3.95, 3.95], color="#2563EB", lw=1.2, ls=":", zorder=3)
ax.plot([7.08, 7.08], [3.95, 7.18], color="#2563EB", lw=1.2, ls=":", zorder=3)
ax.annotate("", xy=(7.4, 7.18), xytext=(7.08, 7.18),
            arrowprops=dict(arrowstyle="->", color="#2563EB", lw=1.2, ls=":"))
ax.text(7.28, 7.28, "gRPC (Verify Role)", ha="center", va="bottom", fontsize=6.8,
        fontweight="bold", color="#1D4ED8",
        bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="#BFDBFE", lw=0.6), zorder=6)

# Corridor 2: Voice Relay Service (y=4.9) -> Lobby Service (y=5.9) for Membership Check
ax.plot([7.4, 7.22], [4.95, 4.95], color="#DC2626", lw=1.2, ls="-", zorder=3)
ax.plot([7.22, 7.22], [4.95, 6.15], color="#DC2626", lw=1.2, ls="-", zorder=3)
ax.annotate("", xy=(7.4, 6.15), xytext=(7.22, 6.15),
            arrowprops=dict(arrowstyle="->", color="#DC2626", lw=1.2, ls="-"))
ax.text(7.35, 6.25, "Verify Room", ha="center", va="bottom", fontsize=6.8,
        fontweight="bold", color="#B91C1C",
        bbox=dict(boxstyle="round,pad=0.15", fc="white", ec="#FECACA", lw=0.6), zorder=6)


# =============================================================================
# 9. MESSAGE BROKER / EVENT BUS (Redis Pub/Sub) & TELEMETRY
# =============================================================================
draw_dashed_box(11.2, 4.3, 1.25, 3.2, label="", bg="#FFF7ED", border="#F97316")
mb_icon = patches.FancyBboxPatch((11.45, 5.75), 0.75, 0.75, boxstyle="round,pad=0.04", fc="#EA580C", ec="#C2410C", lw=1.2, zorder=3)
ax.add_patch(mb_icon)
ax.text(11.825, 6.2, "Redis", ha="center", va="center", fontsize=8, fontweight="bold", color="white", zorder=4)
ax.text(11.825, 5.9, "Pub/Sub", ha="center", va="center", fontsize=7.5, color="#FFEDD5", zorder=4)
ax.text(11.825, 5.2, "Event Bus\n(Lobby State\nSync)", ha="center", va="center", fontsize=6.8, color="#9A3412", zorder=4)

# Publish / Subscribe arrows (horizontal, clean spacing)
ax.annotate("", xy=(11.2, 6.15), xytext=(10.5, 6.15),
            arrowprops=dict(arrowstyle="->", color="#EA580C", lw=1.2))
ax.text(10.85, 6.27, "Publish Event", ha="center", va="bottom", fontsize=6.8, color="#EA580C", fontweight="semibold")

ax.annotate("", xy=(10.5, 5.65), xytext=(11.2, 5.65),
            arrowprops=dict(arrowstyle="->", color="#EA580C", lw=1.2, ls="--"))
ax.text(10.85, 5.53, "Subscribe Event", ha="center", va="top", fontsize=6.8, color="#EA580C")

# Telemetry & Blob Storage
draw_dashed_box(11.2, 1.35, 1.95, 2.3, label="", bg="#F8FAFC", border="#64748B")
stor_box = patches.FancyBboxPatch((11.4, 2.05), 0.55, 0.85, boxstyle="round,pad=0.03", fc="#475569", ec="#334155", lw=1.2, zorder=3)
ax.add_patch(stor_box)
ax.plot([11.45, 11.9], [2.65, 2.65], color="#94A3B8", lw=1.8, zorder=4)
ax.plot([11.45, 11.9], [2.45, 2.45], color="#94A3B8", lw=1.8, zorder=4)
ax.plot([11.45, 11.9], [2.25, 2.25], color="#94A3B8", lw=1.8, zorder=4)

ax.text(12.35, 2.7, "Telemetry & Blob", ha="center", va="center", fontsize=7.5, fontweight="bold", color="#1E293B")
ax.text(12.35, 2.25, "Replay Logs &\nGame Manifests\n(Vercel Blob / S3)", ha="center", va="center", fontsize=6.5, color="#64748B")

ax.annotate("", xy=(11.2, 2.5), xytext=(10.5, 2.5),
            arrowprops=dict(arrowstyle="->", color="#475569", lw=1.1))
ax.text(10.85, 2.62, "Save Telemetry", ha="center", va="bottom", fontsize=6.5, color="#475569")


# =============================================================================
# 10. 3RD PARTY INTEGRATIONS (Right Box)
# =============================================================================
draw_dashed_box(13.8, 1.0, 2.2, 7.2, label="3rd Party", label_pos="top_right", bg="#FAFAFA", border="#94A3B8")

third_parties = [
    ("Unity Gaming Services", "Unity Lobby & Relay\nJWKS Token Auth", 6.9, "#000000", "#1E293B"),
    ("MongoDB Atlas", "Cloud Multi-Region\nPersistent Storage", 5.7, "#13AA52", "#065F46"),
    ("Upstash Redis", "Serverless Memory Cache\nRoom State Machine", 4.5, "#00E599", "#047857"),
    ("Discord Webhook", "Lobby Invite Links\nMatch Alerts & Events", 3.3, "#5865F2", "#3730A3"),
    ("Steamworks SDK", "PC Achievements\nFriend List Presence", 2.1, "#171A21", "#0F172A"),
]

for name, desc, y_pos, bg_badge, fg_text in third_parties:
    badge = patches.FancyBboxPatch((13.95, y_pos - 0.35), 1.9, 0.72, boxstyle="round,pad=0.03",
                                  fc="white", ec="#CBD5E1", lw=1.0, zorder=3)
    ax.add_patch(badge)
    circle = plt.Circle((14.22, y_pos + 0.02), 0.16, fc=bg_badge, ec="none", zorder=4)
    ax.add_patch(circle)
    ax.text(14.22, y_pos + 0.02, name[0], ha="center", va="center", fontsize=8, fontweight="bold", color="white", zorder=5)

    ax.text(14.48, y_pos + 0.12, name, ha="left", va="center", fontsize=7.2, fontweight="bold", color="#0F172A", zorder=4)
    ax.text(14.48, y_pos - 0.12, desc, ha="left", va="center", fontsize=6.0, color="#475569", zorder=4)

# Clean horizontal connections from Server Side to 3rd Party
ax.annotate("", xy=(13.95, 6.9), xytext=(13.5, 6.9),
            arrowprops=dict(arrowstyle="<->", color="#0F172A", lw=1.2))
ax.annotate("", xy=(13.95, 5.7), xytext=(13.5, 5.7),
            arrowprops=dict(arrowstyle="<->", color="#13AA52", lw=1.2))
ax.annotate("", xy=(13.95, 4.5), xytext=(12.45, 4.5),
            arrowprops=dict(arrowstyle="<->", color="#00E599", lw=1.3))
ax.annotate("", xy=(13.95, 3.3), xytext=(13.5, 3.3),
            arrowprops=dict(arrowstyle="->", color="#5865F2", lw=1.2, ls="--"))
ax.annotate("", xy=(13.95, 2.1), xytext=(13.5, 2.1),
            arrowprops=dict(arrowstyle="<->", color="#171A21", lw=1.2))


# =============================================================================
# 11. BOTTOM CAPTION
# =============================================================================
ax.text(8.25, 0.45, "Figure 4.1: Hold My Hand Microservices & Distributed System Architecture",
        ha="center", va="center", fontsize=11.5, fontweight="bold", fontstyle="italic", color="#0F172A")

plt.tight_layout()
plt.savefig(OUT_PATH, bbox_inches="tight", pad_inches=0.1)
plt.close()
print(f"[OK] Re-generated with clean routing & zero overlaps: {OUT_PATH}")
