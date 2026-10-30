# -*- coding: utf-8 -*-
"""
Script tạo FA26SE047_Report3_SRS.docx
Software Requirement Specification - Hold My Hand
"""

from docx import Document
from docx.shared import Pt, RGBColor, Inches, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import os

OUTPUT_PATH = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\FA26SE047_Report3_SRS.docx"

DARK_RED = RGBColor(0xC0, 0x00, 0x00)
BLACK = RGBColor(0x00, 0x00, 0x00)


def set_cell_bg(cell, hex_color="D9D9D9"):
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), hex_color)
    tcPr.append(shd)


def add_table_borders(table):
    tbl = table._tbl
    tblPr = tbl.find(qn('w:tblPr'))
    if tblPr is None:
        tblPr = OxmlElement('w:tblPr')
        tbl.insert(0, tblPr)
    tblBorders = OxmlElement('w:tblBorders')
    for border_name in ['top', 'left', 'bottom', 'right', 'insideH', 'insideV']:
        border = OxmlElement(f'w:{border_name}')
        border.set(qn('w:val'), 'single')
        border.set(qn('w:sz'), '4')
        border.set(qn('w:space'), '0')
        border.set(qn('w:color'), '000000')
        tblBorders.append(border)
    tblPr.append(tblBorders)



def add_diagram(doc, img_name, caption, width_inches=6.0):
    img_path = os.path.join(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams", img_name)
    if os.path.exists(img_path):
        p_img = doc.add_paragraph()
        p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_img.paragraph_format.space_before = Pt(10)
        p_img.paragraph_format.space_after = Pt(4)
        run_img = p_img.add_run()
        run_img.add_picture(img_path, width=Inches(width_inches))
        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_cap.paragraph_format.space_before = Pt(2)
        p_cap.paragraph_format.space_after = Pt(12)
        run_cap = p_cap.add_run(caption)
        run_cap.font.name = "Arial"
        run_cap.font.size = Pt(9.5)
        run_cap.font.bold = True
        run_cap.font.italic = True
        run_cap.font.color.rgb = RGBColor(0x33, 0x41, 0x55)

def add_paragraph(doc, text="", bold=False, italic=False, size=11,
                  color=BLACK, align=WD_ALIGN_PARAGRAPH.LEFT,
                  space_before=0, space_after=6):
    p = doc.add_paragraph()
    p.alignment = align
    p.paragraph_format.space_before = Pt(space_before)
    p.paragraph_format.space_after = Pt(space_after)
    if text:
        run = p.add_run(text)
        run.bold = bold
        run.italic = italic
        run.font.size = Pt(size)
        run.font.color.rgb = color
    return p


def add_heading1(doc, text):
    p = doc.add_paragraph()
    p.style = doc.styles['Heading 1']
    run = p.add_run(text)
    run.font.size = Pt(14)
    run.font.bold = True
    return p


def add_heading2(doc, text):
    p = doc.add_paragraph()
    p.style = doc.styles['Heading 2']
    run = p.add_run(text)
    run.font.size = Pt(13)
    run.font.bold = True
    return p


def add_heading3(doc, text):
    p = doc.add_paragraph()
    p.style = doc.styles['Heading 3']
    run = p.add_run(text)
    run.font.size = Pt(12)
    run.font.bold = True
    return p


def add_heading4(doc, text):
    p = doc.add_paragraph()
    p.style = doc.styles['Heading 4']
    run = p.add_run(text)
    run.font.size = Pt(11)
    run.font.bold = True
    run.font.italic = False
    return p


def add_bullet(doc, text, level=0):
    p = doc.add_paragraph(style='List Bullet')
    p.paragraph_format.left_indent = Inches(0.25 * (level + 1))
    run = p.add_run(text)
    run.font.size = Pt(11)
    return p


def bold_run(para, text, size=11):
    run = para.add_run(text)
    run.bold = True
    run.font.size = Pt(size)
    return run


def normal_run(para, text, size=11):
    run = para.add_run(text)
    run.bold = False
    run.font.size = Pt(size)
    return run


def set_col_width(table, col_idx, width_inches):
    for row in table.rows:
        row.cells[col_idx].width = Inches(width_inches)


def table_header_row(table, headers, bg="4472C4", fg=RGBColor(0xFF, 0xFF, 0xFF)):
    row = table.rows[0]
    for i, h in enumerate(headers):
        cell = row.cells[i]
        set_cell_bg(cell, bg)
        cell.paragraphs[0].clear()
        run = cell.paragraphs[0].add_run(h)
        run.bold = True
        run.font.size = Pt(10)
        run.font.color.rgb = fg
        cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER


def add_table_row(table, values, bold_first=False, font_size=10):
    row = table.add_row()
    for i, val in enumerate(values):
        cell = row.cells[i]
        cell.paragraphs[0].clear()
        run = cell.paragraphs[0].add_run(str(val))
        run.font.size = Pt(font_size)
        if bold_first and i == 0:
            run.bold = True
    return row


def create_document():
    doc = Document()

    # --- Page margins ---
    section = doc.sections[0]
    section.top_margin = Inches(1)
    section.bottom_margin = Inches(1)
    section.left_margin = Inches(1.25)
    section.right_margin = Inches(1)

    # =========================================================
    # COVER PAGE
    # =========================================================
    # Logo & University line
    logo_path = os.path.join(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams", "fpt_logo.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(10)
        p_logo.add_run().add_picture(logo_path, width=Inches(2.2))

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("FPT UNIVERSITY – SOFTWARE ENGINEERING CAPSTONE")
    run.font.size = Pt(12)
    run.bold = True
    run.font.color.rgb = RGBColor(0x00, 0x00, 0x00)

    for _ in range(3):
        add_paragraph(doc, "", size=12)

    # Title 1
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("CAPSTONE PROJECT REPORT")
    run.bold = True
    run.font.size = Pt(28)
    run.font.color.rgb = DARK_RED

    add_paragraph(doc, "", size=10)

    # Title 2
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("Report 3 \u2013 Software Requirement Specification")
    run.bold = True
    run.font.size = Pt(22)
    run.font.color.rgb = DARK_RED

    for _ in range(4):
        add_paragraph(doc, "", size=12)

    # Project info block
    info_lines = [
        ("Project Name: ", "Hold My Hand \u2013 Asymmetric Multiplayer 3D Puzzle Platformer System"),
        ("Project Code: ", "FA26SE047"),
        ("Class: ", "Fall 2026  |  Duration: 09/2026 \u2013 03/2027"),
        ("Group: ", "GFA26SE047 - Hold My Hand Team"),
        ("Supervisor: ", "ThS. Ph\u1ea1m Thanh Tr\xed (tript9@fpt.edu.vn)"),
    ]
    for label, val in info_lines:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(label)
        r.bold = True
        r.font.size = Pt(12)
        r2 = p.add_run(val)
        r2.font.size = Pt(12)

    add_paragraph(doc, "", size=12)

    # Members
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run("Team Members:")
    r.bold = True
    r.font.size = Pt(12)

    members = [
        "\u0110o\u00e0n L\xfd H\u1ed3ng Minh (SE180733) \u2013 PM / Backend Lead",
        "\u0110\u1eb7ng Duy Ph\xfa (SE180727) \u2013 Backend Developer",
        "Tr\u1ea7n Huy D\u01b0\u01a1ng (SE180648) \u2013 Unity Developer / Report 3 Lead",
        "Nguy\u1ec5n Ph\xfa L\u00e2m (SE180388) \u2013 Unity Developer",
    ]
    for m in members:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = p.add_run(m)
        run.font.size = Pt(11)

    for _ in range(4):
        add_paragraph(doc, "", size=12)

    # Date footer
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("\u2013 Ho Chi Minh City, October 2026 \u2013")
    run.font.size = Pt(14)
    run.italic = True

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("Project Code: FA26SE047 | Class: Fall 2026")
    run.font.size = Pt(12)

    # Page break after cover
    doc.add_page_break()

    # =========================================================
    # I. RECORD OF CHANGES
    # =========================================================
    add_heading1(doc, "I. Record of Changes")

    p = doc.add_paragraph()
    p.add_run("A \u2013 Added   M \u2013 Modified   D \u2013 Deleted").italic = True

    table = doc.add_table(rows=1, cols=4)
    add_table_borders(table)
    table_header_row(table, ["Date", "A/M/D", "In Charge", "Change Description"])

    changes = [
        ("10/10/2026", "A", "Tr\u1ea7n Huy D\u01b0\u01a1ng", "Initial draft of Report 3 (SRS v0.9)"),
        ("25/10/2026", "M", "Tr\u1ea7n Huy D\u01b0\u01a1ng", "Update Use Cases, Actor descriptions"),
        ("30/10/2026", "M", "\u0110o\u00e0n L\xfd H\u1ed3ng Minh", "Review & finalize FR/NFR sections"),
    ]
    for row_data in changes:
        add_table_row(table, row_data, font_size=10)

    doc.add_paragraph()

    # =========================================================
    # II. SOFTWARE REQUIREMENT SPECIFICATION
    # =========================================================
    add_heading1(doc, "II. Software Requirement Specification")

    # --- 1. Product Overview ---
    add_heading2(doc, "1. Product Overview")

    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(6)
    run = p.add_run(
        "\u201cHold My Hand\u201d is an Online Co-op 3D Puzzle Platformer ecosystem developed by group FA26SE047. "
        "The system allows two players to connect online and cooperatively solve puzzles and defeat bosses "
        "through an asymmetric gameplay model: one player controls \u201cThe Father\u201d (large, powerful, responsible "
        "for heavy lifting and shielding) and the other controls \u201cThe Child\u201d (small, agile, able to pass "
        "through narrow gaps and activate switches). The system comprises three integrated sub-systems:"
    )
    run.font.size = Pt(11)

    subsystems = [
        "Unity 6 Game Client (Windows .exe): Real-time 3D gameplay engine.",
        "Centralized RESTful Backend (Node.js/Express + MongoDB + Redis): Authentication, Lobby management, Moderation, and Voice relay.",
        "Web Portal & Admin System (ReactJS): Out-game player dashboard, leaderboard, and administrative control.",
    ]
    for i, s in enumerate(subsystems, 1):
        p = doc.add_paragraph(style='List Number')
        run = p.add_run(s)
        run.font.size = Pt(11)

    add_paragraph(doc, "")
    p = doc.add_paragraph()
    r = p.add_run("Context Diagram:")
    add_diagram(doc, "r3_fig1_context_diagram.png", "Figure 3.1: Hold My Hand System Context Diagram", 6.0)
    r.bold = True
    r.font.size = Pt(11)
    r2 = p.add_run(
        " Figure 3.1 depicts the System Context Diagram (Data Flow Diagram Level 0) for Hold My Hand "
        "following the Yourdon & DeMarco notation. The central system process (0: Hold My Hand Information System) "
        "exchanges structured input and output data flows with four primary external entities: "
        "Guest, Authenticated Player, Moderator, and Administrator."
    )
    r2.font.size = Pt(11)
    r2.italic = True

    doc.add_paragraph()

    # --- 2. User Requirements ---
    add_heading2(doc, "2. User Requirements")

    # 2.1 Actors
    add_heading3(doc, "2.1 Actors")

    table = doc.add_table(rows=1, cols=3)
    add_table_borders(table)
    table_header_row(table, ["Actor", "Description", "Examples of Interaction"])

    actors = [
        ("Player (Guest)", "Unauthenticated user browsing the game. Can view login/register screen.",
         "Register new account."),
        ("Player (Authenticated)", "Registered user with JWT token. Can create/join lobbies, choose role (Dad/Child), play co-op levels, use voice chat.",
         "Lobby creation, game session, swap role."),
        ("Moderator", "Staff with elevated role. Can monitor active lobbies, forcibly close/kick sessions, adjust dynamic character stats.",
         "POST /api/mod/lobby/:code/close"),
        ("Admin", "Highest authority. Full user account management, ban/unban, audit log review.",
         "User management, system monitoring."),
        ("System (Backend)", "Automated actors \u2013 JWT auth middleware, lobby state machine, voice relay service, Redis cache.",
         "Token validation, lobby lifecycle management."),
    ]
    for row_data in actors:
        add_table_row(table, row_data, font_size=10)

    doc.add_paragraph()

    # 2.2 Use Cases
    add_heading3(doc, "2.2 Use Cases")

    # 2.2.1 Diagrams
    add_heading4(doc, "2.2.1 Diagram(s)")
    add_diagram(doc, "r3_fig2_usecase_diagram.png", "Figure 3.2: Hold My Hand Overall System Use Case Diagram", 6.2)

    p = doc.add_paragraph()
    run = p.add_run(
        "The system defines 4 main actors (Guest, Authenticated Player, Moderator, Admin) interacting "
        "with the following use case groups:"
    )
    run.font.size = Pt(11)

    uc_groups = [
        ("UC-AUTH", "UC01-Register, UC02-Login, UC03-Logout"),
        ("UC-LOBBY", "UC04-Create Lobby, UC05-Join Lobby, UC06-Swap Role (Dad/Child), UC07-Ready Up, UC08-Leave Lobby"),
        ("UC-GAME", "UC09-Start Game Session, UC10-Co-op Puzzle Interaction, UC11-Combat & Boss Encounter, UC12-Voice Chat (PTT/Auto)"),
        ("UC-MOD", "UC13-Monitor Active Lobbies, UC14-Force Close Lobby, UC15-Adjust Dynamic Stats, UC16-Manage Game Scenes & Stages, UC17-Manage In-Game Items & Props"),
        ("UC-ADMIN", "UC18-Manage User Accounts, UC19-Ban/Unban User, UC20-View Audit Log, UC21-Configure Server & Version Rules"),
        ("UC-WEB", "UC22-View Global Leaderboard, UC23-View Profile & Match History"),
    ]
    for group_id, items in uc_groups:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{group_id}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(items)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    # 2.2.2 Descriptions
    add_heading4(doc, "2.2.2 Descriptions")

    uc_cols = ["Use Case ID", "Use Case Name", "Actor", "Pre-condition", "Main Flow", "Post-condition", "Exception"]
    table = doc.add_table(rows=1, cols=7)
    add_table_borders(table)
    table_header_row(table, uc_cols)

    uc_data = [
        ("UC04", "Create Lobby", "Authenticated Player",
         "Player is logged in",
         "1. Player clicks \u2018Create Lobby\u2019.\n2. System generates 6-character lobby code.\n3. System assigns Player as \u2018Dad\u2019 by default.\n4. Lobby status set to WAITING.",
         "Lobby created, player in lobby.",
         "JWT expired \u2192 redirect to login"),
        ("UC05", "Join Lobby", "Authenticated Player",
         "Player has lobby code",
         "1. Player enters 6-char code.\n2. Backend validates lobby exists and has slot.\n3. Player assigned \u2018Child\u2019 role.",
         "Player joins, both see lobby state.",
         "Lobby full \u2192 409 Conflict"),
        ("UC06", "Swap Role", "Authenticated Player",
         "2 players in lobby, game not started",
         "1. Player clicks \u2018Swap Role\u2019.\n2. Backend swaps Dad\u2194Child roles.\n3. Both players see updated roles.",
         "Roles swapped.",
         "Game already started \u2192 denied"),
        ("UC14", "Force Close Lobby", "Moderator",
         "Moderator authenticated, lobby exists",
         "1. Moderator selects active lobby.\n2. POST /api/mod/lobby/:code/close with reason.\n3. Backend marks lobby CLOSED.\n4. Both players notified.",
         "Lobby closed, players disconnected.",
         "Lobby already closed \u2192 409"),
        ("UC16", "Manage Game Scenes", "Moderator",
         "Moderator authenticated (RBAC MOD/ADMIN)",
         "1. Moderator opens Scene & Stage management.\n2. Modifies puzzle trigger states & scene availability.\n3. Saves and broadcasts update via WebSocket.",
         "Game scenes & stages updated in real time.",
         "Invalid Scene ID \u2192 404"),
        ("UC17", "Manage Items & Props", "Moderator",
         "Moderator authenticated (RBAC MOD/ADMIN)",
         "1. Moderator opens Props & Toys item catalog.\n2. Adjusts interactable properties & spawn rates.\n3. Publishes configuration to Redis cache.",
         "Item properties and toy props synchronized.",
         "Schema mismatch \u2192 400"),
    ]
    for uc in uc_data:
        row = table.add_row()
        for i, val in enumerate(uc):
            cell = row.cells[i]
            cell.paragraphs[0].clear()
            run = cell.paragraphs[0].add_run(val)
            run.font.size = Pt(9)
            if i == 0:
                run.bold = True

    doc.add_paragraph()

    # --- 3. Functional Requirements ---
    add_heading2(doc, "3. Functional Requirements")

    # 3.1
    add_heading3(doc, "3.1 System Functional Overview")

    # 3.1.1
    add_heading4(doc, "3.1.1 Screens Flow")
    add_diagram(doc, "r3_fig3_screen_flow.png", "Figure 3.3: Hold My Hand Screens Navigation Flow (Game Client & Web Portal)", 6.2)


    screen_flows = [
        "Launch \u2192 Splash Screen \u2192 Main Menu",
        "Main Menu \u2192 [Login Screen | Register Screen]",
        "Login \u2192 Lobby Browser \u2192 [Create Lobby | Join Lobby] \u2192 Lobby 3D Room",
        "Lobby 3D Room \u2192 [Character Select + Role Swap + Voice Chat Settings] \u2192 Loading Screen \u2192 Gameplay Scene",
        "Gameplay Scene \u2192 [Puzzle Level 1 \u2192 Puzzle Level 2 \u2192 Boss Fight Level] \u2192 Result Screen \u2192 Main Menu",
        "Web Portal (Separate): Login \u2192 Player Dashboard \u2192 Leaderboard | Achievement Page",
        "Admin Portal: Login \u2192 User Management | Audit Log | Lobby Monitor",
    ]
    for flow in screen_flows:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(flow)
        run.font.size = Pt(11)

    doc.add_paragraph()

    # 3.1.2
    add_heading4(doc, "3.1.2 Screen Descriptions")

    table = doc.add_table(rows=1, cols=3)
    add_table_borders(table)
    table_header_row(table, ["Screen Name", "Description", "Role Access"])

    screens = [
        ("Splash Screen", "Animated \u2018Hold My Hand\u2019 logo intro, checks internet connection", "All"),
        ("Main Menu", "Navigation hub: Play, Settings, Credits, Quit", "All"),
        ("Login Screen", "Email + Password fields, Login button, link to Register", "Guest"),
        ("Register Screen", "Username, Email, Password, Confirm Password, Register button", "Guest"),
        ("Lobby Browser", "List of public lobbies, search by code, Create/Join buttons", "Player"),
        ("Lobby 3D Room", "3D waiting area showing Dad and Child characters, role swap button, ready button, mic button (VOICE), lobby code display", "Player"),
        ("Loading Screen", "Scene transition with progress bar", "Player"),
        ("Game Scene (Puzzle)", "Main 3D gameplay \u2013 asymmetric controls, puzzle elements, synchronized physics", "2 Players"),
        ("Game Scene (Boss)", "Multi-phase boss encounter with asymmetric attack/defense roles", "2 Players"),
        ("Result Screen", "Victory/Defeat display, stats summary, \u2018Return to Lobby\u2019", "Player"),
        ("Web Dashboard", "Player profile, game history, achievement badges", "Player"),
        ("Admin Panel", "User table, ban controls, lobby monitor, audit trail", "Admin/Mod"),
    ]
    for row_data in screens:
        add_table_row(table, row_data, font_size=10)

    doc.add_paragraph()

    # 3.1.3
    add_heading4(doc, "3.1.3 Screen Authorization")

    table = doc.add_table(rows=1, cols=5)
    add_table_borders(table)
    table_header_row(table, ["Screen", "Guest", "Player", "Moderator", "Admin"])

    auth_data = [
        ("Login / Register", "\u2713", "-", "-", "-"),
        ("Lobby Browser", "-", "\u2713", "\u2713", "\u2713"),
        ("Lobby 3D Room", "-", "\u2713", "\u2713 (view)", "\u2713 (view)"),
        ("Game Scene", "-", "\u2713", "-", "-"),
        ("Web Dashboard", "-", "\u2713", "\u2713", "\u2713"),
        ("Mod Panel (Lobby Close)", "-", "-", "\u2713", "\u2713"),
        ("Admin Panel (User Mgmt)", "-", "-", "-", "\u2713"),
    ]
    for row_data in auth_data:
        add_table_row(table, row_data, font_size=10)

    doc.add_paragraph()

    # 3.1.4
    add_heading4(doc, "3.1.4 Non-Screen Functions")

    nsf_items = [
        ("NSF-01", "JWT Authentication Middleware", "Validates Bearer token on every protected endpoint."),
        ("NSF-02", "Lobby State Machine", "Manages transitions: WAITING \u2192 READY \u2192 IN_GAME \u2192 CLOSED."),
        ("NSF-03", "Voice WebSocket Relay",
         "Binary audio forwarding at wss://.../voice; 644-byte frames (4-byte seq + 320 signed int16 PCM, 16kHz mono, 20ms)."),
        ("NSF-04", "Redis / In-Memory Cache", "Stores lobby state with fallback to in-memory when Redis unavailable (Vercel)."),
        ("NSF-05", "Scheduled Lobby Cleanup", "Auto-closes lobbies idle for >30 minutes."),
    ]
    for nsf_id, name, desc in nsf_items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{nsf_id} \u2013 {name}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(desc)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    # 3.1.5
    add_heading4(doc, "3.1.5 Entity Relationship Diagram")
    add_diagram(doc, "r3_fig4_erd.png", "Figure 3.4: Hold My Hand Database Entity Relationship Diagram (ERD & Document Schema)", 6.0)

    p = doc.add_paragraph()
    run = p.add_run("Entities:")
    run.bold = True
    run.font.size = Pt(11)

    entities = [
        "User: {userId (PK), username, email, passwordHash, role (Player/Moderator/Admin), createdAt, isBanned}",
        "Lobby: {lobbyCode (PK), player1Id (FK\u2192User), player2Id (FK\u2192User), player1Role (Dad/Child), player2Role, status (WAITING/READY/IN_GAME/CLOSED), createdAt, closedAt, closedBy}",
        "GameSession: {sessionId (PK), lobbyCode (FK\u2192Lobby), startedAt, endedAt, levelId, result (WIN/LOSE/ABANDONED)}",
        "AuditLog: {logId (PK), actorId (FK\u2192User), action, targetId, details, timestamp}",
    ]
    for e in entities:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(e)
        run.font.size = Pt(11)

    p = doc.add_paragraph()
    run = p.add_run("Relationships:")
    run.bold = True
    run.font.size = Pt(11)

    rels = [
        "User (1) \u2194 (N) Lobby (as player1 or player2)",
        "Lobby (1) \u2192 (1) GameSession",
        "User (1) \u2192 (N) AuditLog",
    ]
    for r in rels:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(r)
        run.font.size = Pt(11)

    doc.add_paragraph()

    # 3.2
    add_heading3(doc, "3.2 Account & Lobby Management (FE-01)")

    add_heading4(doc, "3.2.1 Register (UC01)")
    items = [
        ("Function Trigger", "User clicks \u2018Register\u2019 on Main Menu."),
        ("Actors", "Guest"),
        ("Interface", "Register Screen (Username, Email, Password fields)"),
        ("Data Processing", "POST /api/auth/register \u2192 Hash password (bcrypt) \u2192 Save to MongoDB \u2192 Return success."),
        ("Validation", "Username 3-20 chars alphanumeric; Email valid format; Password min 8 chars; Confirm Password match."),
        ("Error", "Duplicate email \u2192 \u201cEmail already registered\u201d (409)."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.2.2 Login (UC02)")
    items = [
        ("Function Trigger", "User clicks \u2018Login\u2019."),
        ("Data Processing", "POST /api/auth/login \u2192 Verify credentials \u2192 Issue JWT (24h expiry) \u2192 Store in PlayerPrefs (Unity) / localStorage (Web)."),
        ("Error", "Wrong credentials \u2192 401 \u201cInvalid email or password\u201d."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.2.3 Create Lobby (UC04)")
    items = [
        ("Function Trigger", "Player clicks \u2018Create Lobby\u2019 on Lobby Browser."),
        ("Data Processing", "POST /api/game/lobby/create \u2192 Generate unique 6-char alphanumeric code \u2192 Store in Redis/Memory \u2192 Return lobbyCode."),
        ("Default", "Creator assigned Dad role. Status = WAITING."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.2.4 Join Lobby (UC05)")
    items = [
        ("Function Trigger", "Player enters 6-char code and clicks \u2018Join\u2019."),
        ("Data Processing", "POST /api/game/lobby/join {lobbyCode} \u2192 Check lobby exists, status==WAITING, slot available \u2192 Assign Child role \u2192 Return lobby state."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.2.5 Ready Up & Start Game (UC07)")
    items = [
        ("Function Trigger", "Both players click \u2018Ready\u2019."),
        ("Data Processing", "POST /api/game/lobby/:code/ready \u2192 When both players ready, status WAITING\u2192READY. Unity client transitions to GameScene."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    # 3.3
    add_heading3(doc, "3.3 Asymmetric Character System (FE-02)")

    add_heading4(doc, "3.3.1 Dad Character Controller")
    items = [
        ("Role", "\u201cThe Father\u201d \u2013 Large, slow, high strength. Can carry/throw the Child. Physics Capsule Collider radius 0.6m."),
        ("Controls", "WASD move, Space jump, E interact, Hold F = Hold Hand with Child, Right Click = Shield."),
        ("Asymmetric Ability", "Boost Throw \u2013 hold Child then right-click release to throw Child to high platform."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.3.2 Child Character Controller")
    items = [
        ("Role", "\u201cThe Child\u201d \u2013 Small, agile, can crawl through 0.4m gaps, activate child-only switches."),
        ("Controls", "WASD move, Space jump, E interact, Hold F = Hold Hand with Dad."),
        ("Asymmetric Ability", "Crawl (Ctrl) \u2013 passes through narrow tunnels inaccessible to Dad."),
    ]
    for label, val in items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.3.3 Swap Role Mechanism")
    p = doc.add_paragraph(style='List Bullet')
    r1 = p.add_run("In Lobby only: ")
    r1.bold = True
    r1.font.size = Pt(11)
    r2 = p.add_run("POST /api/game/lobby/:code/swap \u2192 Exchanges player1Role\u2194player2Role in backend \u2192 Reflected in Lobby 3D UI.")
    r2.font.size = Pt(11)

    doc.add_paragraph()

    # 3.4
    add_heading3(doc, "3.4 Online Multiplayer & Synchronization (FE-03)")

    add_heading4(doc, "3.4.1 Lobby Networking (Unity Gaming Services)")
    items_347 = [
        "Unity Lobby SDK manages room membership (up to 2 players per session).",
        "6-character lobby code surfaced to players for manual sharing.",
        "Lobby data (player names, roles, ready state) stored both in Unity Lobby and synced with Backend REST API.",
    ]
    for item in items_347:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "3.4.2 Voice Chat System (FE-07 / VOICE-001 to VOICE-005)")
    voice_items = [
        ("Architecture", "Real-time Voice-over-IP via WebSocket relay at wss://hold-my-hand-backend.vercel.app/voice."),
        ("Auth Frame", "Client sends JSON auth frame {type, lobbyId, lobbyCode, playerId, token} on connect."),
        ("Audio Format", "644-byte binary frames (4-byte seq + 320\u00d7int16 PCM, 16kHz mono, 20ms)."),
        ("DSP Processing", "VoiceDSPProcessor.cs applies Formant/Pitch shift (Dad: -4 semitones, Child: +5 semitones) to distinguish voices."),
        ("PTT Mode", "Press V key to transmit; Auto-Gate mode detects voice activity."),
        ("UI", "1-click microphone button in Lobby 3D Room; popup settings for mic device selection."),
        ("Status", "VOICE-001 to VOICE-004 DONE; VOICE-005 (E2E test) BLOCKED pending 2-client hardware test."),
    ]
    for label, val in voice_items:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    # 3.5
    add_heading3(doc, "3.5 Co-op Puzzle & Environmental Interaction (FE-04)")

    add_heading4(doc, "3.5.1 Cooperative Trigger System")
    items_35 = [
        "Puzzle triggers require BOTH players to activate simultaneously or sequentially (configurable via ScriptableObject per puzzle).",
        "Types: Pressure plates (both stand simultaneously), Lever chains (Dad pulls lever, Child activates switch within timer), Hold Hand sequence (both F simultaneously to open hidden path).",
        "Network sync: Trigger state replicated via Unity Netcode NetworkVariable.",
    ]
    for item in items_35:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    # 3.6
    add_heading3(doc, "3.6 Combat & Boss Encounter (FE-05)")

    add_heading4(doc, "3.6.1 Boss Fight System")
    items_36 = [
        "Multi-phase Boss (3 phases): Phase 1 (Dad tanks, Child flanks), Phase 2 (joint switch activation), Phase 3 (Boost Throw Child to hit Boss weak point).",
        "Boss HP synced via NetworkVariable. Both clients see identical Boss state.",
    ]
    for item in items_36:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    # 3.7
    add_heading3(doc, "3.7 Moderator System (FE-06 / MOD-001)")

    add_heading4(doc, "3.7.1 Force Close Lobby")
    items_37 = [
        ("Endpoint", "POST /api/mod/lobby/:code/close"),
        ("Request", "{reason: string, notifyPlayers: boolean}"),
        ("Response", "{success, lobbyCode, previousStatus, currentStatus, closedBy, closedAt, affectedPlayers[]}"),
        ("Unity Client", "ModApiClient.cs CloseLobbyAsync()"),
        ("Status", "Backend endpoint DONE (commit f8cb363 / 34e94e8). Unity-side UGS sync BLOCKED."),
    ]
    for label, val in items_37:
        p = doc.add_paragraph(style='List Bullet')
        r1 = p.add_run(f"{label}: ")
        r1.bold = True
        r1.font.size = Pt(11)
        r2 = p.add_run(val)
        r2.font.size = Pt(11)

    doc.add_paragraph()

    # --- 4. Non-Functional Requirements ---
    add_heading2(doc, "4. Non-Functional Requirements")

    add_heading3(doc, "4.1 External Interfaces")
    external = [
        "Unity Lobby SDK (Unity Gaming Services): Room membership, player data.",
        "Vercel Serverless: Hosts Backend REST API and Voice WebSocket relay.",
        "MongoDB Atlas: Cloud persistent database for users, sessions, audit logs.",
        "Redis (Upstash recommended): Lobby state cache with in-memory fallback.",
    ]
    for item in external:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading3(doc, "4.2 Quality Attributes")

    add_heading4(doc, "4.2.1 Usability")
    usab = [
        "New players should complete lobby creation and join a game within 2 minutes without documentation.",
        "UI text must be in English. Key game controls displayed on HUD.",
        "Voice Chat one-click mic button visible in Lobby Room at all times.",
    ]
    for item in usab:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "4.2.2 Reliability")
    rel = [
        "Availability: Backend API uptime \u226599% on Vercel (serverless auto-scaling).",
        "MTBF: No system crash allowed during normal 2-player co-op session (< 1 crash/100 sessions).",
        "In-Memory Redis Fallback: Backend continues functioning without Redis connection.",
        "Max Defect Rate: < 5 critical bugs per KLOC.",
    ]
    for item in rel:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "4.2.3 Performance")
    perf = [
        "REST API response time: < 300ms (P95) for lobby operations.",
        "Voice Chat latency: < 150ms end-to-end (target for 2 players on same country server).",
        "Network game state sync: < 100ms lag for character position updates.",
        "Concurrent lobbies supported: \u226550 simultaneous active lobbies on Vercel.",
    ]
    for item in perf:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading4(doc, "4.2.4 Security")
    sec = [
        "All protected endpoints require valid JWT Bearer token.",
        "JWT expiry: 24 hours. Refresh required via re-login.",
        "RBAC: Player, Moderator, Admin roles strictly enforced via middleware.",
        "Voice WebSocket: RS256 JWT signature verification against Unity JWKS. PlayerId verified against Unity Lobby membership.",
        "No audio data persisted server-side.",
    ]
    for item in sec:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    # --- 5. Requirement Appendix ---
    add_heading2(doc, "5. Requirement Appendix")

    add_heading3(doc, "5.1 Business Rules")
    table = doc.add_table(rows=1, cols=2)
    add_table_borders(table)
    table_header_row(table, ["Rule ID", "Rule Description"])

    br_data = [
        ("BR-01", "A lobby can have exactly 2 players: 1 Dad role and 1 Child role. No duplicate roles allowed."),
        ("BR-02", "Role swap (Dad\u2194Child) is only allowed when the lobby is in WAITING status, not IN_GAME."),
        ("BR-03", "A game session cannot start unless both players have clicked \u2018Ready\u2019."),
        ("BR-04", "Moderator can force-close any lobby regardless of game state with a mandatory reason string."),
        ("BR-05", "Voice Chat authentication requires valid Unity Access Token AND membership in the same Unity Lobby ID."),
        ("BR-06", "Lobby codes expire (auto-close) after 30 minutes of inactivity."),
    ]
    for row_data in br_data:
        add_table_row(table, row_data, bold_first=True, font_size=10)

    doc.add_paragraph()

    add_heading3(doc, "5.2 Common Requirements")
    common = [
        "All datetime values stored and transmitted in ISO 8601 UTC format.",
        "All API responses use Content-Type: application/json.",
        "Game client targets 64-bit Windows PC only (initial release).",
        "Internet connection mandatory for all gameplay features.",
    ]
    for item in common:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    doc.add_paragraph()

    add_heading3(doc, "5.3 Application Messages List")
    table = doc.add_table(rows=1, cols=3)
    add_table_borders(table)
    table_header_row(table, ["Message ID", "Context", "Message Text"])

    msg_data = [
        ("MSG-001", "Login fail", "\u201cInvalid email or password. Please try again.\u201d"),
        ("MSG-002", "Lobby full", "\u201cThis lobby is already full. Please try another code.\u201d"),
        ("MSG-003", "Lobby closed by Mod", "\u201cYour lobby has been closed by a Moderator: {reason}\u201d"),
        ("MSG-004", "Voice auth fail", "\u201cVoice Chat connection failed. Please re-enter the lobby.\u201d"),
        ("MSG-005", "Game start", "\u201cBoth players are ready. Starting game in 3...\u201d"),
    ]
    for row_data in msg_data:
        add_table_row(table, row_data, bold_first=True, font_size=10)

    doc.add_paragraph()

    add_heading3(doc, "5.4 Other Requirements")
    other = [
        "The game client must gracefully handle server disconnection by displaying a reconnection prompt for up to 30 seconds before returning to Main Menu.",
        "Voice Chat is classified as an Optional Scale-up Feature (FE-07). Its absence does not block core gameplay functionality.",
    ]
    for item in other:
        p = doc.add_paragraph(style='List Bullet')
        run = p.add_run(item)
        run.font.size = Pt(11)

    # =========================================================
    # Save
    # =========================================================
    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)
    doc.save(OUTPUT_PATH)
    print(f"[OK] File saved: {OUTPUT_PATH}")


if __name__ == "__main__":
    create_document()
