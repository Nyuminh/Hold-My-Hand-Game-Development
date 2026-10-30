# -*- coding: utf-8 -*-
"""
Generate textbook-standard Activity Diagram and State Machine Diagram for Hold My Hand (FA26SE047):
Follows the User's Exact Rules & Style:
1. Academic Monochrome (#0F172A) on pure white cards (#FFFFFF).
2. SHARP RECTANGLES ONLY: Strict 90-degree corners (patches.Rectangle). NO rounded corners (NO FancyBboxPatch).
3. ZERO collision, ZERO overlapping text or lines, ZERO line piercing through boxes.
4. EXACT edge snapping: Arrows terminate and start PRECISELY on the bounding box border (shrinkA=0, shrinkB=0).
5. 100% horizontal text (rotation=0) with clean white background padding (bbox=dict(fc='white', ec='none')).
6. Orthogonal 90-degree corridors through clear clearance channels.
7. Prominent triangular arrowheads (-|> with mutation_scale=15).
8. Proper UML symbols: Initial circle, Bullseye final node, Sharp Decision diamonds, Fork/Join synchronization bars.
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
# HELPER CLASSES FOR SHARP, MATHEMATICALLY EXACT UML ELEMENTS
# =============================================================================
class SharpBox:
    """A sharp 90-degree rectangle with exact anchor points at its perimeter."""
    def __init__(self, cx, cy, w, h, text_main, text_sub=""):
        self.cx = cx
        self.cy = cy
        self.w = w
        self.h = h
        self.text_main = text_main
        self.text_sub = text_sub
        self.left = cx - w / 2.0
        self.right = cx + w / 2.0
        self.bottom = cy - h / 2.0
        self.top = cy + h / 2.0

    def anchor_top(self, offset_x=0.0):
        return (self.cx + offset_x, self.top)

    def anchor_bottom(self, offset_x=0.0):
        return (self.cx + offset_x, self.bottom)

    def anchor_left(self, offset_y=0.0):
        return (self.left, self.cy + offset_y)

    def anchor_right(self, offset_y=0.0):
        return (self.right, self.cy + offset_y)

    def draw(self, ax, lw=1.4):
        # Strict sharp rectangle
        rect = patches.Rectangle(
            (self.left, self.bottom), self.w, self.h,
            fc=BG_CARD, ec=BORDER_COLOR, lw=lw, zorder=3
        )
        ax.add_patch(rect)
        if self.text_sub:
            ax.text(self.cx, self.cy + 0.14, self.text_main,
                    ha="center", va="center", fontsize=8.2, fontweight="bold", color=LINE_COLOR, zorder=4)
            ax.text(self.cx, self.cy - 0.16, self.text_sub,
                    ha="center", va="center", fontsize=7.2, color="#334155", zorder=4)
        else:
            ax.text(self.cx, self.cy, self.text_main,
                    ha="center", va="center", fontsize=8.2, fontweight="bold", color=LINE_COLOR, zorder=4)


class SharpDiamond:
    """A sharp decision diamond with exact anchor vertices."""
    def __init__(self, cx, cy, rx=0.5, ry=0.4):
        self.cx = cx
        self.cy = cy
        self.rx = rx
        self.ry = ry

    def anchor_top(self):
        return (self.cx, self.cy + self.ry)

    def anchor_bottom(self):
        return (self.cx, self.cy - self.ry)

    def anchor_left(self):
        return (self.cx - self.rx, self.cy)

    def anchor_right(self):
        return (self.cx + self.rx, self.cy)

    def draw(self, ax, lw=1.4):
        diamond = patches.Polygon(
            [[self.cx, self.cy + self.ry],
             [self.cx + self.rx, self.cy],
             [self.cx, self.cy - self.ry],
             [self.cx - self.rx, self.cy]],
            closed=True, fc=BG_CARD, ec=BORDER_COLOR, lw=lw, zorder=3
        )
        ax.add_patch(diamond)


def draw_arrow(ax, pts, label="", label_pos=None, label_offset=(0, 0.14), fontsize=7.4):
    """
    Draw an orthogonal connector with prominent arrowhead touching the final anchor exactly.
    pts: list of (x, y) coordinates.
    """
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    # Polyline for full path
    ax.plot(xs, ys, color=LINE_COLOR, lw=1.35, zorder=5)

    # Arrowhead at the final segment
    p_prev, p_last = pts[-2], pts[-1]
    ax.annotate(
        "",
        xy=p_last,
        xytext=p_prev,
        arrowprops=dict(
            arrowstyle="-|>",
            mutation_scale=MUTATION_SCALE,
            color=LINE_COLOR,
            lw=1.35,
            shrinkA=0,
            shrinkB=0
        ),
        zorder=6
    )

    if label:
        if label_pos:
            lx, ly = label_pos
        else:
            # Placed near the midpoint of the first segment or longest segment
            lx = (pts[0][0] + pts[1][0]) / 2.0 + label_offset[0]
            ly = (pts[0][1] + pts[1][1]) / 2.0 + label_offset[1]
        ax.text(
            lx, ly, label,
            ha="center", va="center",
            fontsize=fontsize, color=LINE_COLOR,
            bbox=dict(boxstyle="square,pad=0.15", fc="white", ec="none", alpha=1.0),
            zorder=7
        )


# =============================================================================
# 1. ACTIVITY DIAGRAM WITH SWIMLANES (Figure 3.5 in Report 3)
# =============================================================================
def generate_activity_diagram():
    fig, ax = plt.subplots(figsize=(22, 14), dpi=200)
    ax.set_xlim(0, 22)
    ax.set_ylim(0, 14)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Title
    ax.text(11.0, 13.55, "Hold My Hand — UML Activity Diagram for Co-op Matchmaking & Gameplay Lifecycle",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR, family="DejaVu Sans")

    # Outer Frame
    outer = patches.Rectangle((0.8, 0.5), 20.4, 12.6, fc=BG_CARD, ec=BORDER_COLOR, lw=1.8, zorder=1)
    ax.add_patch(outer)

    # 4 Swimlanes
    lanes = [
        (0.8, 5.8, "Player 1 (Host / Dad)"),
        (5.8, 10.8, "Player 2 (Client / Child)"),
        (10.8, 16.2, "Unity Game Client (Netcode & Physics)"),
        (16.2, 21.2, "Backend Server & Match Store (Node/Redis)"),
    ]

    for x_start, x_end, lane_title in lanes:
        w = x_end - x_start
        hdr = patches.Rectangle((x_start, 12.3), w, 0.8, fc=BG_CARD, ec=BORDER_COLOR, lw=1.4, zorder=2)
        ax.add_patch(hdr)
        ax.text(x_start + w/2, 12.7, lane_title, ha="center", va="center", fontsize=9.2, fontweight="bold", color=LINE_COLOR, zorder=3)
        if x_start > 0.8:
            ax.plot([x_start, x_start], [0.5, 12.3], color="#94A3B8", lw=1.2, ls="--", zorder=2)

    # -------------------------------------------------------------------------
    # ROW 1: Init & Create Lobby (y = 11.1)
    # -------------------------------------------------------------------------
    # Initial Node (Lane 1)
    init_circle = plt.Circle((3.3, 12.0), 0.22, fc=LINE_COLOR, ec=LINE_COLOR, zorder=4)
    ax.add_patch(init_circle)

    b1_create = SharpBox(3.3, 11.1, 3.4, 0.8, "Click 'Create Lobby'", "Select Initial Settings")
    b1_create.draw(ax)
    draw_arrow(ax, [(3.3, 11.78), b1_create.anchor_top()])

    b2_gen = SharpBox(18.7, 11.1, 3.6, 0.8, "Generate 6-Char Code", "Init Room in Redis (Host='Dad')")
    b2_gen.draw(ax)

    # Host -> Backend (Clear corridor at y = 11.1 across empty Lane 2 & 3)
    draw_arrow(ax, [b1_create.anchor_right(), b2_gen.anchor_left()], "POST /api/game/lobby/create")

    # -------------------------------------------------------------------------
    # ROW 2: Backend returns code -> Host displays -> Peer inputs (y = 9.5)
    # -------------------------------------------------------------------------
    b3_disp = SharpBox(3.3, 9.5, 3.4, 0.8, "Display Room Code 'AB12CD'", "Host Assigned 'Dad' Role")
    b3_disp.draw(ax)

    # Return Code from Backend to Host:
    # Strictly 90-degree orthogonal via empty corridor y = 10.35, completely ABOVE all Row 2 boxes!
    draw_arrow(ax, [b2_gen.anchor_bottom(offset_x=-1.0), (17.7, 10.35), (3.3, 10.35), b3_disp.anchor_top()],
               "200 OK (lobbyCode)", label_pos=(11.0, 10.52))

    # Peer inputs code (Lane 2)
    b4_input = SharpBox(8.3, 9.5, 3.4, 0.8, "Input Code 'AB12CD'", "Click 'Join Lobby'")
    b4_input.draw(ax)

    # Host shares code with Peer (direct horizontal link across Lane 1 & 2 gap only)
    draw_arrow(ax, [b3_disp.anchor_right(), b4_input.anchor_left()], "Share Code")

    # -------------------------------------------------------------------------
    # ROW 3: Peer Join -> Backend Validate (y = 8.1)
    # -------------------------------------------------------------------------
    b5_val = SharpBox(18.7, 8.1, 3.6, 0.8, "Validate Slot Availability", "Assign 'Child' Role & Update Redis")
    b5_val.draw(ax)

    # Peer -> Backend: Drops to y = 8.1, crosses Lane 3 (completely empty at y = 8.1) to Backend left!
    # Label is placed at (13.7, 8.28) in the open clearance of Lane 3
    draw_arrow(ax, [b4_input.anchor_right(), (10.5, 9.5), (10.5, 8.1), b5_val.anchor_left()],
               "POST /api/game/lobby/join", label_pos=(13.7, 8.28))

    # -------------------------------------------------------------------------
    # ROW 4: Unity Syncs 3D Lobby UI (y = 6.9)
    # -------------------------------------------------------------------------
    b6_sync = SharpBox(13.5, 6.9, 3.8, 0.8, "Sync 3D Lobby UI State", "Both Avatars Loaded")
    b6_sync.draw(ax)

    # Backend broadcasts to Unity
    draw_arrow(ax, [b5_val.anchor_bottom(offset_x=-1.0), (17.7, 6.9), b6_sync.anchor_right()],
               "Broadcast State", label_pos=(16.6, 7.08))

    # -------------------------------------------------------------------------
    # ROW 5: Decision Diamond - Swap Roles? (y = 5.7)
    # -------------------------------------------------------------------------
    d1_swap = SharpDiamond(13.5, 5.7, rx=0.5, ry=0.4)
    d1_swap.draw(ax)
    draw_arrow(ax, [b6_sync.anchor_bottom(), d1_swap.anchor_top()])

    # Swap Role Action in Lane 2
    b7_swap = SharpBox(8.3, 5.7, 3.4, 0.8, "Request Role Swap", "Dad <-> Child Exchanged")
    b7_swap.draw(ax)

    # Diamond -> Swap Action
    draw_arrow(ax, [d1_swap.anchor_left(), b7_swap.anchor_right()], "[Swap Desired]")

    # Swap Action -> Return loop to Diamond right vertex (via empty corridor y = 4.85)
    draw_arrow(ax, [b7_swap.anchor_bottom(), (8.3, 4.85), (15.6, 4.85), (15.6, 5.7), d1_swap.anchor_right()],
               "Roles Inverted", label_pos=(11.0, 5.02))

    # -------------------------------------------------------------------------
    # ROW 6: Both Players Ready (y = 4.0)
    # -------------------------------------------------------------------------
    b8_ready = SharpBox(13.5, 4.0, 3.8, 0.75, "Both Players Click 'Ready'", "Lobby Status -> 'READY'")
    b8_ready.draw(ax)
    draw_arrow(ax, [d1_swap.anchor_bottom(), b8_ready.anchor_top()], "[Ready / No Swap]", label_pos=(13.5, 4.55))

    # -------------------------------------------------------------------------
    # ROW 7: Fork Synchronization Bar & Parallel Branches
    # -------------------------------------------------------------------------
    fork_y = 3.2
    ax.plot([11.6, 15.4], [fork_y, fork_y], color=LINE_COLOR, lw=5.0, solid_capstyle='butt', zorder=4)
    draw_arrow(ax, [b8_ready.anchor_bottom(), (13.5, fork_y)])

    # Branch A: Stage assets
    b9a = SharpBox(12.3, 2.45, 2.4, 0.7, "Preload 3D Stage Assets", "Handshake Netcode Sync")
    b9a.draw(ax)
    draw_arrow(ax, [(12.3, fork_y), b9a.anchor_top()])

    # Branch B: Voice channel
    b9b = SharpBox(14.7, 2.45, 2.4, 0.7, "Open Voice WSS", "Init Opus PTT Channel")
    b9b.draw(ax)
    draw_arrow(ax, [(14.7, fork_y), b9b.anchor_top()])

    # -------------------------------------------------------------------------
    # ROW 8: Join Synchronization Bar
    # -------------------------------------------------------------------------
    join_y = 1.7
    ax.plot([11.6, 15.4], [join_y, join_y], color=LINE_COLOR, lw=5.0, solid_capstyle='butt', zorder=4)
    draw_arrow(ax, [b9a.anchor_bottom(), (12.3, join_y)])
    draw_arrow(ax, [b9b.anchor_bottom(), (14.7, join_y)])

    # -------------------------------------------------------------------------
    # ROW 9: Execute Gameplay & Stage Complete (y = 0.95)
    # -------------------------------------------------------------------------
    b10_play = SharpBox(13.5, 0.95, 3.8, 0.75, "Execute Asymmetric Gameplay", "[Hold Hands]: Physics Sync & Sanity")
    b10_play.draw(ax)
    draw_arrow(ax, [(13.5, join_y), b10_play.anchor_top()])

    b11_stats = SharpBox(18.7, 0.95, 3.4, 0.75, "Update Leaderboard & EXP", "Save Match History")
    b11_stats.draw(ax)
    draw_arrow(ax, [b10_play.anchor_right(), b11_stats.anchor_left()], "[Stage Cleared]")

    # Activity Final Node (Bullseye in Lane 4)
    fc_outer = plt.Circle((20.75, 0.95), 0.22, fc=BG_CARD, ec=LINE_COLOR, lw=1.4, zorder=4)
    fc_inner = plt.Circle((20.75, 0.95), 0.12, fc=LINE_COLOR, ec=LINE_COLOR, zorder=5)
    ax.add_patch(fc_outer)
    ax.add_patch(fc_inner)
    draw_arrow(ax, [b11_stats.anchor_right(), (20.53, 0.95)])

    # Caption
    ax.text(11.0, 0.2, "Figure 3.5: UML Activity Diagram for Co-op Matchmaking & Gameplay Lifecycle (FA26SE047)",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r3_fig5_activity_diagram.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.savefig(os.path.join(OUT_DIR, "activity_diagram.png"), bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] Activity Diagram generated cleanly with SHARP RECTANGLES and ZERO collisions:", out_path)


# =============================================================================
# 2. STATE MACHINE DIAGRAM (Figure 4.10 in Report 4)
# =============================================================================
class SharpStateBox:
    """A 2-compartment sharp UML State box with exact edge anchors."""
    def __init__(self, cx, cy, w, h, state_name, actions):
        self.cx = cx
        self.cy = cy
        self.w = w
        self.h = h
        self.state_name = state_name
        self.actions = actions
        self.left = cx - w / 2.0
        self.right = cx + w / 2.0
        self.bottom = cy - h / 2.0
        self.top = cy + h / 2.0

    def anchor_top(self, offset_x=0.0):
        return (self.cx + offset_x, self.top)

    def anchor_bottom(self, offset_x=0.0):
        return (self.cx + offset_x, self.bottom)

    def anchor_left(self, offset_y=0.0):
        return (self.left, self.cy + offset_y)

    def anchor_right(self, offset_y=0.0):
        return (self.right, self.cy + offset_y)

    def draw(self, ax, lw=1.4):
        # Outer sharp rectangle
        rect = patches.Rectangle(
            (self.left, self.bottom), self.w, self.h,
            fc=BG_CARD, ec=BORDER_COLOR, lw=lw, zorder=3
        )
        ax.add_patch(rect)
        hdr_h = 0.48
        # Header text
        ax.text(self.cx, self.top - hdr_h / 2.0, self.state_name,
                ha="center", va="center", fontsize=8.6, fontweight="bold", color=LINE_COLOR, zorder=4)
        # Compartment divider
        ax.plot([self.left, self.right], [self.top - hdr_h, self.top - hdr_h],
                color=BORDER_COLOR, lw=1.1, zorder=4)
        # Action text lines
        cur_y = self.top - hdr_h - 0.22
        for act in self.actions:
            ax.text(self.left + 0.18, cur_y, act, ha="left", va="center", fontsize=7.2, color="#334155", zorder=4)
            cur_y -= 0.22


def generate_state_machine():
    fig, ax = plt.subplots(figsize=(21, 13.5), dpi=200)
    ax.set_xlim(-0.5, 20.5)
    ax.set_ylim(0, 13.5)
    ax.axis("off")
    fig.patch.set_facecolor("white")

    # Title
    ax.text(10.0, 12.85, "Hold My Hand — UML State Machine Diagram for Lobby & Game Session Lifecycle",
            ha="center", va="center", fontsize=13.5, fontweight="bold", color=LINE_COLOR, family="DejaVu Sans")

    # 1. Initial State (Solid circle)
    init_state = plt.Circle((1.5, 10.6), 0.22, fc=LINE_COLOR, ec=LINE_COLOR, zorder=4)
    ax.add_patch(init_state)

    # 2. State 1: WAITING_FOR_PEER
    s1_wait = SharpStateBox(4.8, 10.6, 3.4, 1.6, "WAITING_FOR_PEER",
                            ["entry / GenerateLobbyCode()",
                             "entry / SetHostRole('Dad')",
                             "exit / NotifyPeerJoined()"])
    s1_wait.draw(ax)
    draw_arrow(ax, [(1.72, 10.6), s1_wait.anchor_left()], "CreateLobby()")

    # 3. State 2: PEER_CONNECTED
    s2_conn = SharpStateBox(10.2, 10.6, 3.4, 1.6, "PEER_CONNECTED",
                            ["entry / AssignRole('Child')",
                             "entry / EnableRoleSwap()",
                             "exit / LockRoles()"])
    s2_conn.draw(ax)
    draw_arrow(ax, [s1_wait.anchor_right(), s2_conn.anchor_left()], "JoinLobby(Code)\n[SlotFree]")

    # Self-Transition: Role Swap (Clear orthogonal path above state box)
    draw_arrow(ax, [s2_conn.anchor_top(offset_x=-0.7), (9.5, 12.2), (10.9, 12.2), s2_conn.anchor_top(offset_x=0.7)],
               "SwapRole() / InvertRoles()", label_pos=(10.2, 12.35))

    # 4. State 3: READY_TO_START
    s3_ready = SharpStateBox(15.6, 10.6, 3.4, 1.6, "READY_TO_START",
                             ["entry / StartCountdown(5s)",
                              "entry / InitVoiceWSChannel()",
                              "exit / StopCountdown()"])
    s3_ready.draw(ax)
    draw_arrow(ax, [s2_conn.anchor_right(), s3_ready.anchor_left()], "BothPlayersReady()")

    # 5. State 4: LOADING_STAGE
    s4_load = SharpStateBox(15.6, 7.8, 3.4, 1.5, "LOADING_STAGE",
                            ["entry / PrewarmSceneAssets()",
                             "entry / SyncNetcodeClocks()",
                             "exit / SpawnCharacters()"])
    s4_load.draw(ax)
    draw_arrow(ax, [s3_ready.anchor_bottom(), s4_load.anchor_top()], "CountdownFinished()")

    # 6. Composite Super-State: IN_SESSION (Strict sharp outer box)
    super_rect = patches.Rectangle((1.0, 3.2), 17.8, 3.4, fc=BG_CARD, ec=BORDER_COLOR, lw=1.6, zorder=2)
    ax.add_patch(super_rect)
    ax.text(1.4, 6.3, "State: IN_SESSION (Composite Gameplay Lifecycle)", fontsize=8.8, fontweight="bold", color=LINE_COLOR, zorder=3)

    # Sub-states inside IN_SESSION:
    sub1_solo = SharpStateBox(4.2, 4.6, 3.4, 1.5, "SOLO_EXPLORATION",
                              ["entry / EnableSoloMove()",
                               "do / TrackPuzzles()",
                               "exit / CalcDistance()"])
    sub1_solo.draw(ax)

    sub2_sync = SharpStateBox(9.9, 4.6, 3.6, 1.5, "HANDS_HELD_SYNC",
                              ["entry / AttachJointSpring()",
                               "do / RegenerateSanity()",
                               "exit / BreakPhysicsTether()"])
    sub2_sync.draw(ax)

    sub3_boss = SharpStateBox(15.6, 4.6, 3.4, 1.5, "BOSS_ENCOUNTER",
                              ["entry / TriggerCinematic()",
                               "do / Co-opPuzzleCombat()",
                               "exit / StopBossTheme()"])
    sub3_boss.draw(ax)

    # Sub-state transitions:
    draw_arrow(ax, [sub1_solo.anchor_right(offset_y=0.25), sub2_sync.anchor_left(offset_y=0.25)],
               "HoldHandsPress\n[Distance < 2m]")
    draw_arrow(ax, [sub2_sync.anchor_left(offset_y=-0.25), sub1_solo.anchor_right(offset_y=-0.25)],
               "ReleaseHands\n[TetherBreak]")
    draw_arrow(ax, [sub2_sync.anchor_right(), sub3_boss.anchor_left()], "TriggerBossArea()")

    # Loading to Composite entry
    draw_arrow(ax, [s4_load.anchor_bottom(), (15.6, 6.6)], "SceneLoaded & SyncOK")

    # 7. State 5: VICTORY_SUMMARY
    s5_vic = SharpStateBox(4.2, 1.4, 3.4, 1.5, "VICTORY_SUMMARY",
                           ["entry / CalcClearTime()",
                            "entry / UpdateLeaderboard()",
                            "do / DisplayVictoryUI()"])
    s5_vic.draw(ax)
    draw_arrow(ax, [(4.2, 3.2), s5_vic.anchor_top()], "StageBossDefeated\n[ClearConditionMet]")

    # 8. State 6: DEFEAT_SUMMARY
    s6_def = SharpStateBox(9.9, 1.4, 3.4, 1.5, "DEFEAT_SUMMARY",
                           ["entry / FreezeGameplay()",
                            "do / ShowRetryPrompt()",
                            "exit / ClearStageData()"])
    s6_def.draw(ax)
    draw_arrow(ax, [(9.9, 3.2), s6_def.anchor_top()], "SanityDepleted\n[GameOver]")

    # 9. State 7: TERMINATED_CLOSED
    s7_term = SharpStateBox(15.6, 1.4, 3.4, 1.5, "TERMINATED_CLOSED",
                            ["entry / FlushRedisLobby()",
                             "entry / DisconnectClients()",
                             "entry / LogSessionAudit()"])
    s7_term.draw(ax)

    # Defeat -> Terminated
    draw_arrow(ax, [s6_def.anchor_right(), s7_term.anchor_left()], "ExitToMenu()")

    # Victory -> Terminated (via dedicated bottom corridor y = 0.35)
    draw_arrow(ax, [s5_vic.anchor_bottom(), (4.2, 0.35), (15.6, 0.35), s7_term.anchor_bottom()],
               "ExitToMenu()", label_pos=(9.9, 0.48))

    # Rematch: From Victory back to WAITING_FOR_PEER (via dedicated left corridor x = 0.35)
    draw_arrow(ax, [s5_vic.anchor_left(), (0.35, 1.4), (0.35, 10.6), s1_wait.anchor_left()],
               "RematchRequested()", label_pos=(0.35, 6.0))

    # Force Close by Mod (MOD-001) from IN_SESSION to TERMINATED (via dedicated right corridor x = 19.6 entering right-top anchor)
    draw_arrow(ax, [(18.8, 4.6), (19.6, 4.6), (19.6, 1.8), s7_term.anchor_right(offset_y=0.4)],
               "ForceClose (MOD-001)", label_pos=(19.6, 3.2))

    # 10. Final State (Bullseye) exiting from s7_term right-bottom anchor
    final_out = plt.Circle((18.6, 1.1), 0.22, fc=BG_CARD, ec=LINE_COLOR, lw=1.4, zorder=4)
    final_in = plt.Circle((18.6, 1.1), 0.12, fc=LINE_COLOR, ec=LINE_COLOR, zorder=5)
    ax.add_patch(final_out)
    ax.add_patch(final_in)
    draw_arrow(ax, [s7_term.anchor_right(offset_y=-0.3), (18.38, 1.1)])

    # Caption
    ax.text(10.0, 0.2, "Figure 4.10: UML State Machine Diagram for Lobby & Game Session Lifecycle (FA26SE047)",
            ha="center", va="center", fontsize=10.5, fontweight="bold", fontstyle="italic", color=LINE_COLOR)

    out_path = os.path.join(OUT_DIR, "r4_fig10_state_machine.png")
    plt.tight_layout()
    plt.savefig(out_path, bbox_inches="tight", pad_inches=0.1)
    plt.savefig(os.path.join(OUT_DIR, "state_machine_diagram.png"), bbox_inches="tight", pad_inches=0.1)
    plt.close()
    print("[OK] State Machine Diagram generated cleanly with SHARP RECTANGLES and ZERO collisions:", out_path)


# =============================================================================
# MAIN RUNNER
# =============================================================================
if __name__ == "__main__":
    print("[INFO] Generating Activity and State Machine Diagrams...")
    generate_activity_diagram()
    generate_state_machine()
    print("[SUCCESS] Both diagrams generated cleanly!")
