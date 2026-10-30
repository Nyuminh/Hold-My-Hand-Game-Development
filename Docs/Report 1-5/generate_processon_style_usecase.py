# -*- coding: utf-8 -*-
"""
Generate textbook-standard, clean monochrome UML Use Case Diagram for Hold My Hand (FA26SE047):
- Incorporates future-proof Moderator scope:
  * Monitor Active Lobbies (with <<extend>> Force Close Lobby)
  * Adjust Dynamic Character Stats (Live balancing)
  * Manage Game Scenes & Stages (Stage triggers, scene states, checkpoints)
  * Manage In-Game Items & Props (Interactive toy props, item spawns, puzzle objects)
- Balanced 2-sided symmetrical layout:
  * Left: Guest & Player
  * Right: Moderator & Administrator
- Zero crossing lines, zero overlapping ovals, true ellipses (patches.Ellipse).
- Monochrome/slate academic aesthetic (no rainbow colors, no legend/note box).
"""
import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)
OUT_PATH = os.path.join(OUT_DIR, "r3_fig2_usecase_diagram.png")

fig, ax = plt.subplots(figsize=(23, 14), dpi=200)
ax.set_xlim(0, 23)
ax.set_ylim(0, 14)
ax.axis("off")
fig.patch.set_facecolor("white")

# ── Title at top ─────────────────────────────────────────────────────────────
ax.text(11.5, 13.5, "Hold My Hand System — Complete UML 2.5 Use Case Diagram", ha="center", va="center",
        fontsize=16, fontweight="bold", color="#0F172A", family="DejaVu Sans")

# ── System Boundary Box ──────────────────────────────────────────────────────
boundary = patches.Rectangle((3.2, 0.4), 16.6, 12.6, fc="#FFFFFF", ec="#0F172A", lw=1.8, zorder=1)
ax.add_patch(boundary)
ax.text(3.6, 12.65, "System Boundary: Hold My Hand (FA26SE047)", ha="left", va="center",
        fontsize=12.0, fontweight="bold", color="#1E293B", zorder=2)


# =============================================================================
# HELPER FUNCTIONS
# =============================================================================
def draw_stick_actor(ax, x, y, name, scale=1.0):
    r = 0.22 * scale
    head = plt.Circle((x, y + 0.48 * scale), r, fc="white", ec="#0F172A", lw=1.5, zorder=10)
    ax.add_patch(head)
    ax.plot([x, x], [y + 0.26 * scale, y - 0.25 * scale], color="#0F172A", lw=1.5, zorder=10)
    ax.plot([x - 0.35 * scale, x + 0.35 * scale], [y + 0.08 * scale, y + 0.08 * scale], color="#0F172A", lw=1.5, zorder=10)
    ax.plot([x, x - 0.28 * scale], [y - 0.25 * scale, y - 0.65 * scale], color="#0F172A", lw=1.5, zorder=10)
    ax.plot([x, x + 0.28 * scale], [y - 0.25 * scale, y - 0.65 * scale], color="#0F172A", lw=1.5, zorder=10)
    ax.text(x, y - 0.88 * scale, name, ha="center", va="top",
            fontsize=9.5 * scale, fontweight="bold", color="#0F172A", zorder=10)

def draw_usecase_oval(ax, x, y, text, w=2.45, h=0.74):
    ell = patches.Ellipse((x, y), w, h, fc="white", ec="#0F172A", lw=1.4, zorder=5)
    ax.add_patch(ell)
    lines = text.split("\n")
    if len(lines) == 1:
        ax.text(x, y, lines[0], ha="center", va="center", fontsize=8.0, color="#0F172A", zorder=6)
    elif len(lines) == 2:
        ax.text(x, y + 0.09, lines[0], ha="center", va="center", fontsize=7.8, color="#0F172A", zorder=6)
        ax.text(x, y - 0.09, lines[1], ha="center", va="center", fontsize=7.8, color="#0F172A", zorder=6)
    elif len(lines) == 3:
        ax.text(x, y + 0.15, lines[0], ha="center", va="center", fontsize=7.2, color="#0F172A", zorder=6)
        ax.text(x, y, lines[1], ha="center", va="center", fontsize=7.2, color="#0F172A", zorder=6)
        ax.text(x, y - 0.15, lines[2], ha="center", va="center", fontsize=7.2, color="#0F172A", zorder=6)
    return (x - w/2, y), (x + w/2, y)

def draw_arrow(ax, x1, y1, x2, y2, ls="-", stereotype="", color="#0F172A", label_y_offset=0.12):
    arrow = dict(arrowstyle="->", color=color, lw=1.2, ls=ls, shrinkA=0, shrinkB=0)
    ax.annotate("", xy=(x2, y2), xytext=(x1, y1), arrowprops=arrow, zorder=3)
    if stereotype:
        mx = (x1 + x2) / 2
        my = (y1 + y2) / 2 + label_y_offset
        ax.text(mx, my, stereotype, ha="center", va="center", fontsize=7.2, fontstyle="italic",
                fontweight="normal", color=color,
                bbox=dict(boxstyle="square,pad=0.1", fc="white", ec="none", alpha=0.9), zorder=7)


# =============================================================================
# LEFT SIDE: GUEST & PLAYER
# =============================================================================
# Column 1 (Primary Use Cases): x = 5.6, half_width = 1.25 -> left = 4.35, right = 6.85
# Column 2 (Sub Use Cases / Includes & Extends): x = 9.8, half_width = 1.25 -> left = 8.55, right = 11.05

# ── 1. GUEST ACTOR (Top-Left) ────────────────────────────────────────────────
draw_stick_actor(ax, 1.5, 11.5, "Guest", scale=1.0)

# Direct Use Cases for Guest:
draw_usecase_oval(ax, 5.6, 12.1, "Register\nAccount", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 11.6, 4.35, 12.1)

draw_usecase_oval(ax, 5.6, 10.9, "Login\n(JWT Auth)", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 11.4, 4.35, 10.9)


# ── 2. PLAYER ACTOR (Bottom-Left) ────────────────────────────────────────────
draw_stick_actor(ax, 1.5, 6.2, "Player", scale=1.05)

# Primary Use Case 1: Create Lobby
draw_usecase_oval(ax, 5.6, 9.4, "Create Lobby\n(6-Char Code)", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 6.6, 4.35, 9.4)

# Primary Use Case 2: Join Lobby
draw_usecase_oval(ax, 5.6, 8.1, "Join Lobby\nby Code", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 6.4, 4.35, 8.1)

# Swap Role -> OPTIONAL extension! Extends into Join Lobby
draw_usecase_oval(ax, 9.8, 8.1, "Swap Role\n(Dad <-> Child)", w=2.45, h=0.74)
draw_arrow(ax, 8.55, 8.1, 6.85, 8.1, ls="--", stereotype="<<extend>>")


# Primary Use Case 3: Play Co-op Match (Core gameplay loop)
draw_usecase_oval(ax, 5.6, 5.3, "Play Co-op\nMatch", w=2.45, h=0.78)
draw_arrow(ax, 1.9, 6.1, 4.35, 5.3)

# Mandatory sub-usecase 1: Control Character & Movement -> <<include>>
draw_usecase_oval(ax, 9.8, 6.7, "Control Character\n& Sync Netcode", w=2.45, h=0.74)
draw_arrow(ax, 6.85, 5.5, 8.55, 6.7, ls="--", stereotype="<<include>>")

# Mandatory sub-usecase 2: Solve Co-op Puzzle -> <<include>>
draw_usecase_oval(ax, 9.8, 5.6, "Solve Co-op Puzzle\n(Hold Hand / Sync)", w=2.45, h=0.74)
draw_arrow(ax, 6.85, 5.35, 8.55, 5.6, ls="--", stereotype="<<include>>")

# Optional extension 1: Combat Toy Boss Encounter -> <<extend>>
draw_usecase_oval(ax, 9.8, 4.5, "Combat Toy Boss\nEncounter", w=2.45, h=0.74)
draw_arrow(ax, 8.55, 4.5, 6.85, 5.2, ls="--", stereotype="<<extend>>")

# Optional extension 2: Real-Time Voice Chat (PTT & DSP) -> <<extend>>
draw_usecase_oval(ax, 9.8, 3.4, "Real-Time Voice\nChat (PTT & DSP)", w=2.45, h=0.74)
draw_arrow(ax, 8.55, 3.4, 6.85, 5.0, ls="--", stereotype="<<extend>>")


# Primary Use Case 4: View Profile & History
draw_usecase_oval(ax, 5.6, 2.0, "View Profile &\nMatch History", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 5.9, 4.35, 2.0)

# Primary Use Case 5: View Global Leaderboard
draw_usecase_oval(ax, 5.6, 0.9, "View Global\nLeaderboard", w=2.45, h=0.74)
draw_arrow(ax, 1.9, 5.7, 4.35, 0.9)


# =============================================================================
# RIGHT SIDE: MODERATOR & ADMINISTRATOR (SEPARATED!)
# =============================================================================
# Column 1 (Primary Use Cases): x = 17.4, half_width = 1.25 -> left = 16.15, right = 18.65
# Column 2 (Sub Use Cases / Includes & Extends): x = 13.2, half_width = 1.25 -> left = 11.95, right = 14.45

# ── 3. MODERATOR ACTOR (Top-Right) ───────────────────────────────────────────
draw_stick_actor(ax, 21.5, 10.3, "Moderator", scale=1.0)

# Primary Use Case 1: Monitor Active Lobbies
draw_usecase_oval(ax, 17.4, 12.0, "Monitor Active\nLobbies", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 10.6, 18.65, 12.0)

# Extension: Force Close Lobby (MOD-001) -> ONLY when idle or violating!
draw_usecase_oval(ax, 13.2, 12.0, "Force Close Lobby\n(MOD-001)", w=2.45, h=0.74)
draw_arrow(ax, 14.45, 12.0, 16.15, 12.0, ls="--", stereotype="<<extend>>")

# Primary Use Case 2: Adjust Dynamic Character Stats (Live balancing)
draw_usecase_oval(ax, 17.4, 10.8, "Adjust Dynamic\nCharacter Stats", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 10.4, 18.65, 10.8)

# Primary Use Case 3: Manage Game Scenes & Stages (Future Stage & Trigger Management)
draw_usecase_oval(ax, 17.4, 9.6, "Manage Game\nScenes & Stages", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 10.2, 18.65, 9.6)

# Primary Use Case 4: Manage In-Game Items & Props (Future Toys, Props & Drop Rates)
draw_usecase_oval(ax, 17.4, 8.4, "Manage In-Game\nItems & Props", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 10.0, 18.65, 8.4)


# ── 4. ADMINISTRATOR ACTOR (Bottom-Right) ────────────────────────────────────
draw_stick_actor(ax, 21.5, 4.6, "Administrator", scale=1.0)

# Primary Use Case 1: Manage User Accounts
draw_usecase_oval(ax, 17.4, 6.2, "Manage User\nAccounts", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 4.9, 18.65, 6.2)

# Extension: Ban / Unban Players -> ONLY triggered upon reports/violations!
draw_usecase_oval(ax, 13.2, 6.2, "Ban / Unban\nPlayers", w=2.45, h=0.74)
draw_arrow(ax, 14.45, 6.2, 16.15, 6.2, ls="--", stereotype="<<extend>>")

# Primary Use Case 2: View System Audit Logs
draw_usecase_oval(ax, 17.4, 4.6, "View System\nAudit Logs", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 4.6, 18.65, 4.6)

# Primary Use Case 3: Configure Server & Game Rules
draw_usecase_oval(ax, 17.4, 3.0, "Configure Server\n& Version Rules", w=2.45, h=0.74)
draw_arrow(ax, 21.1, 4.3, 18.65, 3.0)


# =============================================================================
# FOOTER CAPTION
# =============================================================================
ax.text(11.5, 0.16, "Figure 3.2: Formal UML 2.5 Use Case Diagram with Full Moderator & Gameplay Scope — Hold My Hand (FA26SE047)",
        ha="center", va="center", fontsize=11.0, fontweight="bold", fontstyle="italic", color="#0F172A")

plt.tight_layout()
plt.savefig(OUT_PATH, bbox_inches="tight", pad_inches=0.1)
plt.close()
print(f"[OK] Diagram with full Moderator scope generated: {OUT_PATH}")
