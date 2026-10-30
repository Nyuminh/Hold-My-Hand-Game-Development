# -*- coding: utf-8 -*-
"""
Generate a clean, formal, academic UML Use Case Diagram for Hold My Hand (FA26SE047).
Strict Requirements:
1. No flashy / rainbow colors: Monochrome / elegant academic slate palette (white, charcoal, subtle grey).
2. Complete coverage: All 4 Actors (Guest, Player, Moderator, Administrator) and all 20 Use Cases (UC01 to UC20).
3. Zero line crossings: Orthogonal and direct horizontal bus connections so no lines cross or overlap.
4. Standard UML notation: Standard stick-figure actors, standard oval use cases, system boundary, <<include>> / <<extend>>.
"""
import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUT_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
os.makedirs(OUT_DIR, exist_ok=True)
OUT_PATH = os.path.join(OUT_DIR, "r3_fig2_usecase_diagram.png")
OUT_DETAILED_PATH = os.path.join(OUT_DIR, "usecase_diagram_clean.png")

fig, ax = plt.subplots(figsize=(19, 12), dpi=200)
ax.set_xlim(0, 19)
ax.set_ylim(0, 12)
ax.axis("off")
fig.patch.set_facecolor("white")

# ── Title at top ─────────────────────────────────────────────────────────────
ax.text(0.8, 11.5, "USE CASE DIAGRAM — HOLD MY HAND SYSTEM", fontsize=15, fontweight="bold", color="#0F172A", family="DejaVu Sans")
ax.text(0.8, 11.15, "Project Code: FA26SE047 | Architecture Specification (Report 3 - SRS Section 2.2)", fontsize=10, color="#475569", family="DejaVu Sans")

# ── System Boundary ──────────────────────────────────────────────────────────
# Boundary box: x = 3.0 to 16.0, y = 0.6 to 11.3 (w = 13.0, h = 10.7)
sys_box = patches.Rectangle((3.0, 0.6), 13.0, 10.2, fc="#FAFAFA", ec="#0F172A", lw=1.5, zorder=1)
ax.add_patch(sys_box)
ax.text(3.3, 10.5, "System Boundary: Hold My Hand System", fontsize=11, fontweight="bold", color="#0F172A", zorder=2)


# =============================================================================
# HELPER FUNCTIONS: ACTOR & USE CASE
# =============================================================================
def draw_uml_actor(x, y, name, role=""):
    # Head
    head = plt.Circle((x, y + 0.35), 0.16, fc="white", ec="#0F172A", lw=1.4, zorder=5)
    ax.add_patch(head)
    # Spine
    ax.plot([x, x], [y + 0.19, y - 0.18], color="#0F172A", lw=1.4, zorder=5)
    # Arms
    ax.plot([x - 0.22, x + 0.22], [y + 0.05, y + 0.05], color="#0F172A", lw=1.4, zorder=5)
    # Legs
    ax.plot([x, x - 0.18], [y - 0.18, y - 0.45], color="#0F172A", lw=1.4, zorder=5)
    ax.plot([x, x + 0.18], [y - 0.18, y - 0.45], color="#0F172A", lw=1.4, zorder=5)
    # Text
    ax.text(x, y - 0.62, f"<<actor>>\n{name}", ha="center", va="top",
            fontsize=8.5, fontweight="bold", color="#0F172A", zorder=5)
    if role:
        ax.text(x, y - 0.95, f"({role})", ha="center", va="top",
                fontsize=7.2, color="#475569", zorder=5)

def draw_usecase(x, y, ucid, title, w=2.4, h=0.55):
    # Standard UML Oval
    oval = patches.FancyBboxPatch((x - w/2, y - h/2), w, h,
                                  boxstyle="round,pad=0.15",
                                  fc="white", ec="#1E293B", lw=1.2, zorder=4)
    ax.add_patch(oval)
    ax.text(x, y + 0.07, ucid, ha="center", va="center",
            fontsize=7.8, fontweight="bold", color="#0F172A", zorder=5)
    ax.text(x, y - 0.11, title, ha="center", va="center",
            fontsize=7.2, color="#1E293B", zorder=5)
    return (x - w/2, y), (x + w/2, y)  # left and right anchor points

def draw_package_box(x, y, w, h, title):
    # Clean thin boundary for functional package
    pkg = patches.FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.04",
                                 fc="#FFFFFF", ec="#94A3B8", lw=0.9, ls="--", zorder=2)
    ax.add_patch(pkg)
    ax.text(x + 0.15, y + h - 0.18, f"Package: {title}", fontsize=7.8,
            fontweight="bold", color="#475569", zorder=3)


# =============================================================================
# PACKAGE BOXES (3 COLUMNS INSIDE SYSTEM BOUNDARY)
# =============================================================================
# Column 1: Client Authentication, Lobby & Web (x = 3.3 to 7.1)
draw_package_box(3.3, 8.4, 3.8, 1.8, "Authentication (UC-AUTH)")
draw_package_box(3.3, 3.8, 3.8, 4.4, "Lobby & Matchmaking (UC-LOBBY)")
draw_package_box(3.3, 0.9, 3.8, 2.7, "Web Portal & Stats (UC-WEB)")

# Column 2: In-Game Cooperative Gameplay (x = 7.4 to 11.6)
draw_package_box(7.4, 0.9, 4.2, 9.3, "In-Game Co-op Gameplay (UC-GAME)")

# Column 3: Moderation & Administration (x = 11.9 to 15.7)
draw_package_box(11.9, 5.7, 3.8, 4.5, "Moderation Controls (UC-MOD)")
draw_package_box(11.9, 0.9, 3.8, 4.6, "Administration (UC-ADMIN)")


# =============================================================================
# COLUMN 1: USE CASES (Left Column)
# =============================================================================
# Auth Package
uc01_L, uc01_R = draw_usecase(5.2, 9.6, "UC01", "Register Account")
uc02_L, uc02_R = draw_usecase(5.2, 8.8, "UC02", "Login (JWT Authentication)")
uc03_L, uc03_R = draw_usecase(5.2, 8.0, "UC03", "Logout")

# Lobby Package
uc04_L, uc04_R = draw_usecase(5.2, 7.3, "UC04", "Create Lobby (6-Char Code)")
uc05_L, uc05_R = draw_usecase(5.2, 6.5, "UC05", "Join Lobby by Code")
uc06_L, uc06_R = draw_usecase(5.2, 5.7, "UC06", "Swap Role (Dad <-> Child)")
uc07_L, uc07_R = draw_usecase(5.2, 4.9, "UC07", "Ready & Start Session")
uc08_L, uc08_R = draw_usecase(5.2, 4.1, "UC08", "Leave Lobby")

# Web Portal Package
uc19_L, uc19_R = draw_usecase(5.2, 2.6, "UC19", "View Player Dashboard & Stats")
uc20_L, uc20_R = draw_usecase(5.2, 1.6, "UC20", "View Global Leaderboards")


# =============================================================================
# COLUMN 2: IN-GAME GAMEPLAY USE CASES (Center Column)
# =============================================================================
uc09_L, uc09_R = draw_usecase(9.5, 8.8, "UC09", "Control Asymmetric Character")
uc10_L, uc10_R = draw_usecase(9.5, 6.8, "UC10", "Solve Co-op Puzzle (Hold Hand / Throw)")
uc11_L, uc11_R = draw_usecase(9.5, 4.8, "UC11", "Combat Toy Boss Encounter")
uc12_L, uc12_R = draw_usecase(9.5, 2.8, "UC12", "Real-Time Voice Chat (PTT & DSP)")

# Internal Gameplay Connections (clean vertical dashed arrows with <<include>>)
# UC09 (Control Character) is included in UC10 (Puzzle) and UC11 (Combat)
ax.annotate("", xy=(9.5, 7.15), xytext=(9.5, 8.45),
            arrowprops=dict(arrowstyle="->", color="#334155", lw=1.1, ls="--"))
ax.text(9.55, 7.8, "<<include>>", ha="left", va="center", fontsize=7.0, color="#475569", fontstyle="italic")

ax.annotate("", xy=(9.5, 5.15), xytext=(9.5, 6.45),
            arrowprops=dict(arrowstyle="->", color="#334155", lw=1.1, ls="--"))
ax.text(9.55, 5.8, "<<include>>", ha="left", va="center", fontsize=7.0, color="#475569", fontstyle="italic")

# UC12 (Voice Chat) extends UC10 & UC11
ax.annotate("", xy=(9.5, 4.45), xytext=(9.5, 3.15),
            arrowprops=dict(arrowstyle="->", color="#334155", lw=1.1, ls="--"))
ax.text(9.55, 3.8, "<<extend>>", ha="left", va="center", fontsize=7.0, color="#475569", fontstyle="italic")

# Connect Lobby Session Start (UC07) to Gameplay (UC09) cleanly via orthogonal line
# (5.2 + 1.2 = 6.4) -> (6.8, 4.9) -> up to 8.8 -> (9.5 - 1.2 = 8.3)
ax.plot([6.4, 7.1, 7.1], [4.9, 4.9, 8.8], color="#334155", lw=1.1, ls="--")
ax.annotate("", xy=(8.3, 8.8), xytext=(7.1, 8.8),
            arrowprops=dict(arrowstyle="->", color="#334155", lw=1.1, ls="--"))
ax.text(7.6, 9.0, "<<include>>", ha="center", va="bottom", fontsize=7.0, color="#475569", fontstyle="italic")


# =============================================================================
# COLUMN 3: MODERATION & ADMINISTRATION USE CASES (Right Column)
# =============================================================================
# Moderation Package
uc13_L, uc13_R = draw_usecase(13.8, 9.2, "UC13", "View Active Lobbies & Sessions")
uc14_L, uc14_R = draw_usecase(13.8, 8.0, "UC14", "Force Close Lobby (MOD-001)")
uc15_L, uc15_R = draw_usecase(13.8, 6.8, "UC15", "Adjust Dynamic Character Stats")

# Administration Package
uc16_L, uc16_R = draw_usecase(13.8, 4.6, "UC16", "Manage User Accounts (Ban/Unban)")
uc17_L, uc17_R = draw_usecase(13.8, 3.4, "UC17", "View System Audit Logs")
uc18_L, uc18_R = draw_usecase(13.8, 2.0, "UC18", "Configure Server & Version Rules")

# Administrator includes all Moderator capabilities:
# Moderator -> Administrator generalization line is drawn outside on the right.


# =============================================================================
# ACTORS & CLEAN NON-OVERLAPPING CONNECTORS
# =============================================================================

# ── ACTOR 1: GUEST (Top-Left) ────────────────────────────────────────────────
draw_uml_actor(1.4, 9.3, "Guest", "Unauthenticated")

# Guest connects only to UC01 (Register) and UC02 (Login)
# Direct horizontal clean lines - ZERO OVERLAP
ax.plot([1.7, 4.0], [9.6, 9.6], color="#0F172A", lw=1.1)  # To UC01
ax.plot([1.7, 4.0], [9.0, 8.8], color="#0F172A", lw=1.1)  # To UC02


# ── ACTOR 2: PLAYER (Mid-Left) ───────────────────────────────────────────────
draw_uml_actor(1.4, 5.7, "Player", "Authenticated Client")

# Player connects to: UC03, UC04, UC05, UC06, UC07, UC08, UC19, UC20
# We use a clean vertical distribution rail at x = 2.4 so NO lines cross or overlap!
ax.plot([1.7, 2.4], [5.7, 5.7], color="#0F172A", lw=1.2)  # Main trunk line from Player
# Vertical rail spanning from y = 1.6 to y = 8.0
ax.plot([2.4, 2.4], [1.6, 8.0], color="#0F172A", lw=1.2)

# Horizontal parallel branches into each use case from the rail:
player_targets = [
    (8.0, uc03_L[0]),  # UC03 Logout
    (7.3, uc04_L[0]),  # UC04 Create Lobby
    (6.5, uc05_L[0]),  # UC05 Join Lobby
    (5.7, uc06_L[0]),  # UC06 Swap Role
    (4.9, uc07_L[0]),  # UC07 Ready Up
    (4.1, uc08_L[0]),  # UC08 Leave Lobby
    (2.6, uc19_L[0]),  # UC19 View Dashboard
    (1.6, uc20_L[0]),  # UC20 View Leaderboard
]
for y_branch, x_target in player_targets:
    ax.plot([2.4, x_target], [y_branch, y_branch], color="#0F172A", lw=1.1)


# ── ACTOR 3: MODERATOR (Mid-Right) ───────────────────────────────────────────
draw_uml_actor(17.6, 8.0, "Moderator", "Staff Role")

# Moderator connects to: UC13, UC14, UC15
# Clean vertical rail at x = 16.6
ax.plot([17.2, 16.6], [8.0, 8.0], color="#0F172A", lw=1.2)  # Trunk line
ax.plot([16.6, 16.6], [6.8, 9.2], color="#0F172A", lw=1.2)  # Vertical rail

mod_targets = [
    (9.2, uc13_R[0]),  # UC13 View Lobbies
    (8.0, uc14_R[0]),  # UC14 Force Close
    (6.8, uc15_R[0]),  # UC15 Dynamic Stats
]
for y_branch, x_target in mod_targets:
    ax.plot([16.6, x_target], [y_branch, y_branch], color="#0F172A", lw=1.1)


# ── ACTOR 4: ADMINISTRATOR (Bottom-Right) ────────────────────────────────────
draw_uml_actor(17.6, 3.4, "Administrator", "System Superuser")

# Administrator connects to: UC16, UC17, UC18
# Clean vertical rail at x = 16.6
ax.plot([17.2, 16.6], [3.4, 3.4], color="#0F172A", lw=1.2)  # Trunk line
ax.plot([16.6, 16.6], [2.0, 4.6], color="#0F172A", lw=1.2)  # Vertical rail

admin_targets = [
    (4.6, uc16_R[0]),  # UC16 Manage Accounts
    (3.4, uc17_R[0]),  # UC17 Audit Logs
    (2.0, uc18_R[0]),  # UC18 Server Config
]
for y_branch, x_target in admin_targets:
    ax.plot([16.6, x_target], [y_branch, y_branch], color="#0F172A", lw=1.1)

# UML Generalization: Administrator inherits from Moderator
# Solid line with hollow white triangle pointing up to Moderator
ax.plot([17.6, 17.6], [4.4, 6.7], color="#0F172A", lw=1.2)
triangle = patches.Polygon([[17.6, 7.1], [17.5, 6.7], [17.7, 6.7]],
                           closed=True, fc="white", ec="#0F172A", lw=1.2, zorder=6)
ax.add_patch(triangle)
ax.text(17.75, 5.5, "<<generalize>>", ha="left", va="center", fontsize=6.8, color="#475569", fontstyle="italic")


# =============================================================================
# FOOTER CAPTION
# =============================================================================
ax.text(9.5, 0.25, "Figure 3.2: Complete System Use Case Diagram — Hold My Hand (FA26SE047)",
        ha="center", va="center", fontsize=11, fontweight="bold", fontstyle="italic", color="#0F172A")

plt.tight_layout()
plt.savefig(OUT_PATH, bbox_inches="tight", pad_inches=0.1)
plt.savefig(OUT_DETAILED_PATH, bbox_inches="tight", pad_inches=0.1)
plt.close()
print(f"[OK] Generated Clean Use Case Diagram: {OUT_PATH}")

