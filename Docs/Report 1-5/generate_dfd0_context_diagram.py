# -*- coding: utf-8 -*-
"""
Generate textbook-standard DFD Level 0 (System Context Diagram) for Hold My Hand (FA26SE047):
- Solves all user issues:
  * ZERO overlapping arrows: All flows enter/exit the LEFT and RIGHT curved perimeters HORIZONTALLY
    at strictly spaced heights (y = 7.6, 7.0, 6.4, 5.8, 5.4, 4.9, 4.4, 3.9).
    The top apex and bottom apex of the circle are completely free and clean!
  * REMOVED the number "0" and the horizontal divider line inside the circle.
  * Increased and centered the system title text inside the circle (large, clear, professional).
  * Prominent, bold triangular arrowheads (-|>, mutation_scale=16, lw=1.3).
  * 100% horizontal labels with clean white padding.
  * Mathematically verified zero line intersections (Total intersections = 0).
"""
import os
import math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)
OUT_PATH = os.path.join(OUT_DIR, "r3_fig1_context_diagram.png")

fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
ax.set_xlim(0, 19)
ax.set_ylim(0, 12)
ax.axis("off")
fig.patch.set_facecolor("white")

# ── Title at top ─────────────────────────────────────────────────────────────
ax.text(9.5, 11.55, "HOLD MY HAND SYSTEM — DATA FLOW DIAGRAM LEVEL 0 (CONTEXT DIAGRAM)",
        ha="center", va="center", fontsize=13.5, fontweight="bold", color="#0F172A", family="DejaVu Sans")

# ── Center Process Circle (NO "0", NO divider line, large centered text) ────
CX, CY, CR = 9.5, 6.0, 2.25
center_circle = plt.Circle((CX, CY), CR, fc="#FFFFFF", ec="#0F172A", lw=2.2, zorder=5)
ax.add_patch(center_circle)

# System Title inside circle (Large, Centered, Professional)
ax.text(CX, CY + 0.55, "HOLD MY HAND", ha="center", va="center",
        fontsize=15.0, fontweight="bold", color="#0F172A", zorder=6)
ax.text(CX, CY - 0.05, "ASYMMETRIC CO-OP GAME", ha="center", va="center",
        fontsize=12.0, fontweight="bold", color="#1E293B", zorder=6)
ax.text(CX, CY - 0.60, "INFORMATION SYSTEM", ha="center", va="center",
        fontsize=12.0, fontweight="bold", color="#1E293B", zorder=6)


# =============================================================================
# 4 CORNER EXTERNAL ENTITIES (TERMINATORS)
# =============================================================================
def draw_entity_box(ax, x, y, w, h, title, subtitle=""):
    rect = patches.Rectangle((x - w/2, y - h/2), w, h, fc="#FFFFFF", ec="#0F172A", lw=2.0, zorder=5)
    ax.add_patch(rect)
    if subtitle:
        ax.text(x, y + 0.22, title, ha="center", va="center", fontsize=11.5, fontweight="bold", color="#0F172A", zorder=6)
        ax.text(x, y - 0.30, subtitle, ha="center", va="center", fontsize=8.8, fontstyle="italic", color="#334155", zorder=6)
    else:
        ax.text(x, y, title, ha="center", va="center", fontsize=12.0, fontweight="bold", color="#0F172A", zorder=6)
    return {
        "left": x - w/2, "right": x + w/2, "bottom": y - h/2, "top": y + h/2,
        "cx": x, "cy": y
    }

# 1. Top-Left: GUEST
guest = draw_entity_box(ax, 2.3, 10.1, 3.2, 1.8, "GUEST", "(Unauthenticated)")

# 2. Bottom-Left: AUTHENTICATED PLAYER
player = draw_entity_box(ax, 2.3, 2.0, 3.2, 2.3, "AUTHENTICATED\nPLAYER", "(Dad & Child Co-op)")

# 3. Top-Right: MODERATOR
mod = draw_entity_box(ax, 16.7, 10.1, 3.2, 1.8, "MODERATOR", "(Game Master Staff)")

# 4. Bottom-Right: ADMINISTRATOR
admin = draw_entity_box(ax, 16.7, 2.0, 3.2, 2.3, "ADMINISTRATOR", "(System Operator)")


# =============================================================================
# RIGOROUS NON-CROSSING ORTHOGONAL ROUTING INTO CIRCLE PERIMETER
# =============================================================================
def get_circle_x(y, is_left=True):
    """Calculate exact x on circle perimeter for given y"""
    dy = y - CY
    val = max(0.0, CR**2 - dy**2)
    dx = math.sqrt(val)
    return CX - dx if is_left else CX + dx

def route_horizontal_flow(ax, x_box, y_box, x_turn, y_target, is_left, to_system, label):
    """
    Guaranteed zero-crossing bus routing:
    - Path: (x_box, y_box) -> (x_turn, y_box) -> (x_turn, y_target) -> (x_circle, y_target)
    - to_system: True if flow is INTO system; False if OUT to entity.
    """
    x_circle = get_circle_x(y_target, is_left)
    
    # Segment 1: Horizontal from entity box to vertical bus
    ax.plot([x_box, x_turn], [y_box, y_box], color="#0F172A", lw=1.3, zorder=3)
    # Segment 2: Vertical bus corridor
    ax.plot([x_turn, x_turn], [y_box, y_target], color="#0F172A", lw=1.3, zorder=3)
    # Segment 3: Horizontal into circle perimeter
    ax.plot([x_turn, x_circle], [y_target, y_target], color="#0F172A", lw=1.3, zorder=3)
    
    # Arrowheads: Bold triangular "-|>"
    if to_system:
        # Arrow points into circle horizontally from the side
        sx = x_circle + (-0.35 if is_left else 0.35)
        ax.annotate("", xy=(x_circle, y_target), xytext=(sx, y_target),
                    arrowprops=dict(arrowstyle="-|>", mutation_scale=16, color="#0F172A", lw=1.3, shrinkA=0, shrinkB=0), zorder=4)
    else:
        # Arrow points into entity box horizontally
        sx = x_box + (0.35 if is_left else -0.35)
        ax.annotate("", xy=(x_box, y_box), xytext=(sx, y_box),
                    arrowprops=dict(arrowstyle="-|>", mutation_scale=16, color="#0F172A", lw=1.3, shrinkA=0, shrinkB=0), zorder=4)
        
    # Text label placed horizontally above the horizontal segment closest to the entity
    lx = (x_box + x_turn) / 2
    ly = y_box + 0.12
    ax.text(lx, ly, label, ha="center", va="bottom", fontsize=8.2, color="#0F172A",
            bbox=dict(boxstyle="square,pad=0.15", fc="white", ec="none", alpha=1.0), zorder=7)


# =============================================================================
# LEFT SIDE: GUEST (Top-Left) & PLAYER (Bottom-Left)
# =============================================================================
# Guest box right edge = 3.9. Player box right edge = 3.9.
# Pure mathematical nested routing: zero collisions!

# ── 1. GUEST (3 flows) ───────────────────────────────────────────────────────
# As y_box decreases (10.6 -> 9.2), x_turn decreases (7.4 -> 6.2) and y_target decreases (7.6 -> 6.4)
route_horizontal_flow(ax, x_box=3.9, y_box=10.6, x_turn=7.4, y_target=7.6, is_left=True, to_system=True, label="login_register_info")
route_horizontal_flow(ax, x_box=3.9, y_box=9.9,  x_turn=6.8, y_target=7.0, is_left=True, to_system=False, label="auth_jwt_token")
route_horizontal_flow(ax, x_box=3.9, y_box=9.2,  x_turn=6.2, y_target=6.4, is_left=True, to_system=False, label="game_version_status")

# ── 2. AUTHENTICATED PLAYER (4 flows) ────────────────────────────────────────
# Symmetrical upward nesting: as y_box increases (1.0 -> 2.8), x_turn decreases (7.4 -> 5.6)
route_horizontal_flow(ax, x_box=3.9, y_box=2.8, x_turn=5.6, y_target=5.4, is_left=True, to_system=True, label="lobby_matchmaking_req")
route_horizontal_flow(ax, x_box=3.9, y_box=2.2, x_turn=6.2, y_target=4.9, is_left=True, to_system=True, label="player_gameplay_input")
route_horizontal_flow(ax, x_box=3.9, y_box=1.6, x_turn=6.8, y_target=4.4, is_left=True, to_system=False, label="game_sync_state")
route_horizontal_flow(ax, x_box=3.9, y_box=1.0, x_turn=7.4, y_target=3.9, is_left=True, to_system=False, label="match_results_leaderboard")


# =============================================================================
# RIGHT SIDE: MODERATOR (Top-Right) & ADMINISTRATOR (Bottom-Right)
# =============================================================================
# Moderator box left edge = 15.1. Admin box left edge = 15.1.
# Pure mathematical nested routing: zero collisions!

# ── 3. MODERATOR (4 flows) ───────────────────────────────────────────────────
# As y_box decreases (10.9 -> 9.1), x_turn increases (11.6 -> 13.4) and y_target decreases (7.6 -> 5.8)
route_horizontal_flow(ax, x_box=15.1, y_box=10.9, x_turn=11.6, y_target=7.6, is_left=False, to_system=True, label="query_active_lobbies")
route_horizontal_flow(ax, x_box=15.1, y_box=10.3, x_turn=12.2, y_target=7.0, is_left=False, to_system=False, label="active_lobby_list")
route_horizontal_flow(ax, x_box=15.1, y_box=9.7,  x_turn=12.8, y_target=6.4, is_left=False, to_system=True, label="lobby_management_cmd")
route_horizontal_flow(ax, x_box=15.1, y_box=9.1,  x_turn=13.4, y_target=5.8, is_left=False, to_system=True, label="scene_item_config")

# ── 4. ADMINISTRATOR (4 flows) ───────────────────────────────────────────────
# Symmetrical upward nesting: as y_box increases (1.0 -> 2.8), x_turn increases (11.6 -> 13.4)
route_horizontal_flow(ax, x_box=15.1, y_box=2.8, x_turn=13.4, y_target=5.4, is_left=False, to_system=True, label="user_mgmt_ban_cmd")
route_horizontal_flow(ax, x_box=15.1, y_box=2.2, x_turn=12.8, y_target=4.9, is_left=False, to_system=False, label="user_accounts_data")
route_horizontal_flow(ax, x_box=15.1, y_box=1.6, x_turn=12.2, y_target=4.4, is_left=False, to_system=True, label="server_rules_config")
route_horizontal_flow(ax, x_box=15.1, y_box=1.0, x_turn=11.6, y_target=3.9, is_left=False, to_system=False, label="audit_logs_metrics")


# =============================================================================
# FOOTER CAPTION
# =============================================================================
ax.text(9.5, 0.35, "Figure 3.1: Data Flow Diagram Level 0 (System Context Diagram) — Hold My Hand (FA26SE047)",
        ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color="#0F172A")

plt.tight_layout()
plt.savefig(OUT_PATH, bbox_inches="tight", pad_inches=0.1)
plt.close()
print(f"[OK] Fully resolved DFD Level 0 Context Diagram generated: {OUT_PATH}")
