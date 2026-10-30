# -*- coding: utf-8 -*-
"""
Script tạo FA26SE047_Report4_SDD.docx
Report 4 – Software Design Document
Project: Hold My Hand (FA26SE047)
"""

from docx import Document
from docx.shared import Pt, RGBColor, Inches, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import copy

OUTPUT_PATH = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\FA26SE047_Report4_SDD.docx"

DARK_RED = RGBColor(0xC0, 0x00, 0x00)
BLACK = RGBColor(0x00, 0x00, 0x00)


def set_cell_bg(cell, color_hex: str):
    """Set background color of a table cell."""
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), color_hex)
    tcPr.append(shd)



import os

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

def add_heading(doc, text, level):
    """Add a heading with the given level."""
    h = doc.add_heading(text, level=level)
    h.alignment = WD_ALIGN_PARAGRAPH.LEFT
    for run in h.runs:
        run.font.color.rgb = BLACK
    return h


def add_cover_line(doc, text, size, bold=False, color=None, italic=False):
    """Add a centered cover page line."""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run(text)
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    if color:
        run.font.color.rgb = color
    return p


def add_table(doc, headers, rows, header_bg="4F4F4F"):
    """Add a formatted table."""
    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.style = 'Table Grid'

    # Header row
    hdr_row = table.rows[0]
    for i, h in enumerate(headers):
        cell = hdr_row.cells[i]
        cell.text = h
        cell.paragraphs[0].runs[0].font.bold = True
        cell.paragraphs[0].runs[0].font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
        cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
        set_cell_bg(cell, header_bg)

    # Data rows
    for ri, row_data in enumerate(rows):
        row = table.rows[ri + 1]
        for ci, cell_text in enumerate(row_data):
            cell = row.cells[ci]
            cell.text = cell_text
            cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.LEFT

    return table


def add_code_block(doc, code_text):
    """Add a monospace code block paragraph."""
    p = doc.add_paragraph()
    run = p.add_run(code_text)
    run.font.name = 'Courier New'
    run.font.size = Pt(10)
    p.paragraph_format.left_indent = Inches(0.4)
    # Light shading
    pPr = p._p.get_or_add_pPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), 'F2F2F2')
    pPr.append(shd)
    return p


def add_bullet(doc, text, level=0):
    p = doc.add_paragraph(style='List Bullet')
    p.add_run(text)
    return p


def add_numbered(doc, text):
    p = doc.add_paragraph(style='List Number')
    p.add_run(text)
    return p


def build_document():
    doc = Document()

    # ── Page margins ──────────────────────────────────────────────────────────
    section = doc.sections[0]
    section.top_margin = Cm(2.5)
    section.bottom_margin = Cm(2.5)
    section.left_margin = Cm(3.0)
    section.right_margin = Cm(2.0)

    # ═════════════════════════════════════════════════════════════════════════
    # COVER PAGE
    # ═════════════════════════════════════════════════════════════════════════
    for _ in range(3):
        doc.add_paragraph()

    add_cover_line(doc, "FPT UNIVERSITY – SOFTWARE ENGINEERING", 14, bold=False)
    doc.add_paragraph()

    logo_path = os.path.join(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams", "fpt_logo.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(10)
        p_logo.add_run().add_picture(logo_path, width=Inches(2.2))
    add_cover_line(doc, "CAPSTONE PROJECT REPORT", 28, bold=True, color=DARK_RED)
    doc.add_paragraph()

    add_cover_line(doc, "Report 4 – Software Design Document", 22, bold=True, color=DARK_RED)
    doc.add_paragraph()
    doc.add_paragraph()

    add_cover_line(doc, "Project: Hold My Hand", 16, bold=True)
    add_cover_line(doc, "Asymmetric Multiplayer 3D Puzzle Platformer System", 14)
    doc.add_paragraph()

    add_cover_line(doc, "Project Code: FA26SE047 | Class: Fall 2026", 13)
    add_cover_line(doc, "Duration: 09/2026 – 03/2027", 13)
    doc.add_paragraph()

    add_cover_line(doc, "Group: GFA26SE047 – Hold My Hand Team", 13, bold=True)
    doc.add_paragraph()

    # Team members table on cover
    members_table = doc.add_table(rows=6, cols=3)
    members_table.style = 'Table Grid'
    members_table.alignment = WD_ALIGN_PARAGRAPH.CENTER

    member_headers = ["Full Name", "Student ID", "Role"]
    member_data = [
        ("Đoàn Lý Hồng Minh", "SE180733", "PM / Backend Lead"),
        ("Đặng Duy Phú",       "SE180727", "Backend Developer"),
        ("Trần Huy Dương",     "SE180648", "Unity Developer"),
        ("Nguyễn Phú Lâm",    "SE180388", "Unity Dev / Report 4 Lead"),
    ]
    for i, h in enumerate(member_headers):
        cell = members_table.rows[0].cells[i]
        cell.text = h
        cell.paragraphs[0].runs[0].font.bold = True
        cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
        set_cell_bg(cell, "C00000")
        cell.paragraphs[0].runs[0].font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)

    for ri, (name, sid, role) in enumerate(member_data):
        row = members_table.rows[ri + 1]
        row.cells[0].text = name
        row.cells[1].text = sid
        row.cells[2].text = role
        for cell in row.cells:
            cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER

    # Supervisor line
    doc.add_paragraph()
    p_sup = doc.add_paragraph()
    p_sup.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p_sup.add_run("Supervisor: ThS. Phạm Thanh Trí (tript9@fpt.edu.vn)")
    r.font.size = Pt(13)
    r.font.italic = True

    doc.add_paragraph()
    add_cover_line(doc, "– Ho Chi Minh City, October 2026 –", 14)

    # Page break after cover
    doc.add_page_break()

    # ═════════════════════════════════════════════════════════════════════════
    # I. RECORD OF CHANGES
    # ═════════════════════════════════════════════════════════════════════════
    add_heading(doc, "I. Record of Changes", 1)
    p_note = doc.add_paragraph()
    p_note.add_run("A – Added    M – Modified    D – Deleted").font.italic = True

    change_headers = ["Date", "A/M/D", "In charge", "Change Description"]
    change_rows = [
        ("12/10/2026", "A", "Nguyễn Phú Lâm",    "Initial draft of Report 4 (SDD v0.9)"),
        ("20/10/2026", "M", "Đoàn Lý Hồng Minh", "Add System Architecture, Package Diagram"),
        ("28/10/2026", "M", "Đặng Duy Phú",       "Update Database Design, Backend Sequence Diagrams"),
        ("30/10/2026", "M", "Nguyễn Phú Lâm",    "Finalize Voice Chat & Unity Client Class Diagrams"),
    ]
    add_table(doc, change_headers, change_rows)
    doc.add_paragraph()

    # ═════════════════════════════════════════════════════════════════════════
    # II. SOFTWARE DESIGN DOCUMENT
    # ═════════════════════════════════════════════════════════════════════════
    add_heading(doc, "II. Software Design Document", 1)

    # ─── 1. System Design ────────────────────────────────────────────────────
    add_heading(doc, "1. System Design", 2)

    # 1.1 System Architecture
    add_heading(doc, "1.1 System Architecture", 3)
    add_diagram(doc, "r4_fig1_system_architecture.png", "Figure 4.1: Hold My Hand Multi-Tier Distributed Software Architecture", 6.2)
    doc.add_paragraph(
        "The Hold My Hand system follows a Client-Server architecture with three main sub-systems:"
    )

    # Sub-system 1
    p = doc.add_paragraph(style='List Number')
    r = p.add_run("Unity Game Client")
    r.font.bold = True
    p.add_run(
        " (Windows .exe): Built on Unity 6 (6000.5.10f1) using the URP rendering pipeline. "
        "Assembly structure: Blocks.Common, Blocks.Gameplay.Core, Blocks.Gameplay.Platformer, "
        "Blocks.Gameplay.Shooter, Blocks.Sessions. Connects to Backend via ApiClient.cs "
        "(UnityWebRequest + JWT). Voice Chat is handled by VoiceChatManager.cs using native "
        "C# WebSocket (System.Net.WebSockets)."
    )

    p2 = doc.add_paragraph(style='List Number')
    r2 = p2.add_run("Backend API")
    r2.font.bold = True
    p2.add_run(
        " (Node.js 18+ ESM + Express 4): RESTful API with JWT RBAC. MongoDB persistence + "
        "Redis / In-Memory cache for lobby state. Deployed on Vercel serverless. Voice relay "
        "implemented as a Vercel Serverless Function (api/voice.js)."
    )

    p3 = doc.add_paragraph(style='List Number')
    r3 = p3.add_run("Web Portal & Admin")
    r3.font.bold = True
    p3.add_run(
        " (ReactJS): Out-game player dashboard and administrative panel, communicates with "
        "the Backend REST API via Axios."
    )

    doc.add_paragraph()
    doc.add_paragraph("Architecture Diagram:").runs[0].font.bold = True
    add_code_block(doc,
        "[Unity Client (Win64)]\n"
        "  ├── ApiClient.cs ──REST──► [Backend: Express + MongoDB/Redis on Vercel]\n"
        "  ├── ModApiClient.cs ──REST (Mod/Admin)──► [Backend /api/mod & /api/admin]\n"
        "  └── VoiceChatManager.cs ──WSS──► [api/voice.js (Vercel Function)]\n"
        "\n"
        "[Web Portal (ReactJS)]\n"
        "  └── Axios ──REST──► [Backend REST API]\n"
        "\n"
        "[Backend]\n"
        "  ├── /api/auth          (JWT issuance)\n"
        "  ├── /api/game/lobby    (Lobby CRUD, State Machine)\n"
        "  ├── /api/mod           (Moderator controls)\n"
        "  ├── /api/admin         (User management)\n"
        "  └── /voice             (WebSocket Relay Function)"
    )

    # 1.2 Package Diagram
    add_heading(doc, "1.2 Package Diagram", 3)
    add_diagram(doc, "r4_fig2_package_diagram.png", "Figure 4.2: Hold My Hand Software Package Structure Diagram", 6.0)
    doc.add_paragraph("Unity Client Packages:").runs[0].font.bold = True
    unity_pkgs = [
        ("Blocks.Common",            "Shared DTOs, utility classes, API client base classes (ApiClient.cs, ModApiClient.cs, AdminApiClient.cs)."),
        ("Blocks.Gameplay.Core",     "Core gameplay logic, character controllers, NetworkManager, VoiceChatManager, VoiceDSPProcessor."),
        ("Blocks.Gameplay.Platformer","Platformer-specific mechanics (jump, hold hand, swing, seesaw)."),
        ("Blocks.Gameplay.Shooter",  "Shooter/puzzle mechanics (energy projectiles, remote switch activation)."),
        ("Blocks.Sessions",          "Lobby lifecycle, matchmaking, Lobby3DManager, Lobby3DScreen UI."),
    ]
    for name, desc in unity_pkgs:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(name + ": ")
        r.font.bold = True
        p.add_run(desc)

    doc.add_paragraph()
    doc.add_paragraph("Backend Modules:").runs[0].font.bold = True
    backend_pkgs = [
        ("routes/auth.js",      "Login, Register endpoints."),
        ("routes/game.js",      "Lobby CRUD, swap, ready."),
        ("routes/mod.js",       "Force close lobby (MOD-001)."),
        ("routes/admin.js",     "User management."),
        ("services/voiceRelay.js","WebSocket room management."),
        ("api/voice.js",        "Vercel serverless function wrapper for voiceRelay."),
        ("middleware/auth.js",  "JWT verify + RBAC authorize."),
        ("store/lobbyStore.js", "Redis/In-Memory lobby state."),
    ]
    for name, desc in backend_pkgs:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(name + ": ")
        r.font.bold = True
        p.add_run(desc)

    doc.add_paragraph()
    doc.add_paragraph("Package Summary Table:").runs[0].font.bold = True
    pkg_headers = ["Package", "Sub-system", "Responsibility"]
    pkg_rows = [
        ("Blocks.Common",              "Unity",   "Shared models, HTTP client wrappers, JWT handling"),
        ("Blocks.Gameplay.Core",       "Unity",   "Gameplay loop, character physics, voice processing"),
        ("Blocks.Gameplay.Platformer", "Unity",   "Platformer mechanics assembly"),
        ("Blocks.Gameplay.Shooter",    "Unity",   "Shooter puzzle mechanics assembly"),
        ("Blocks.Sessions",            "Unity",   "Lobby 3D UI, matchmaking, scene transitions"),
        ("routes/",                    "Backend", "RESTful endpoint handlers"),
        ("services/",                  "Backend", "Business logic (voiceRelay, lobbyStore)"),
        ("middleware/",                "Backend", "JWT auth, RBAC, error handling"),
        ("api/",                       "Backend", "Vercel serverless entry points"),
    ]
    add_table(doc, pkg_headers, pkg_rows)
    doc.add_paragraph()

    # ─── 2. Database Design ──────────────────────────────────────────────────
    add_heading(doc, "2. Database Design", 2)
    add_diagram(doc, "r4_fig3_db_schema.png", "Figure 4.3: Hold My Hand Database Schema & Redis Cache Structure Diagram", 6.0)
    doc.add_paragraph(
        "The backend uses MongoDB (document store) for persistence and Redis "
        "(or in-memory fallback) for real-time lobby state."
    )

    add_heading(doc, "MongoDB Collections", 3)
    db_headers = ["Collection", "Key Fields", "Description"]
    db_rows = [
        ("users",
         "_id, username, email, passwordHash, role, isBanned, createdAt",
         "Player account data"),
        ("gameSessions",
         "_id, lobbyCode, player1Id, player2Id, levelId, result, startedAt, endedAt",
         "Completed game session records"),
        ("auditLogs",
         "_id, actorId, action, targetId, details, timestamp",
         "Moderator/Admin action audit trail"),
    ]
    add_table(doc, db_headers, db_rows)
    doc.add_paragraph()

    doc.add_paragraph("Collection Details:").runs[0].font.bold = True
    coll_details = [
        ("users", "Stores registered player accounts. `role` field is enum: \"Player\" | \"Moderator\" | \"Admin\". `passwordHash` uses bcrypt (12 rounds)."),
        ("gameSessions", "Created when lobby transitions to IN_GAME status. Records final game result and level completed."),
        ("auditLogs", "Every Moderator/Admin action (lobby close, user ban) writes an audit log entry for accountability."),
    ]
    for name, desc in coll_details:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(name + ": ")
        r.font.bold = True
        p.add_run(desc)

    doc.add_paragraph()
    add_heading(doc, "Redis / In-Memory Lobby Store", 3)
    doc.add_paragraph("Key: lobby:{lobbyCode} (JSON object)")
    lobby_headers = ["Field", "Type", "Description"]
    lobby_rows = [
        ("lobbyCode",  "string",        "6-character alphanumeric lobby identifier"),
        ("player1",    "PlayerSlot obj","{ id, username, role: 'Dad' }"),
        ("player2",    "PlayerSlot obj","{ id, username, role: 'Child' } or null"),
        ("status",     "string enum",   "WAITING | READY | IN_GAME | CLOSED"),
        ("createdAt",  "ISO 8601",      "Lobby creation timestamp"),
        ("closedAt",   "ISO 8601",      "Lobby close timestamp (null if open)"),
        ("closedBy",   "string",        "userId of actor who closed the lobby"),
    ]
    add_table(doc, lobby_headers, lobby_rows)
    doc.add_paragraph()

    p_note2 = doc.add_paragraph()
    r_note = p_note2.add_run("Production Note: ")
    r_note.font.bold = True
    p_note2.add_run(
        "In-memory store (fallback) does not share state between Vercel serverless instances. "
        "Upstash Redis is required for multi-instance consistency in production. "
        "Redis TTL is set to 30 minutes per lobby key."
    )

    # ─── 3. Detailed Design ──────────────────────────────────────────────────
    add_heading(doc, "3. Detailed Design", 2)

    # ── 3.1 Account & Lobby Management ────────────────────────────────────
    add_heading(doc, "3.1 Account & Lobby Management (FE-01)", 3)

    # 3.1.1 Class Diagram
    add_heading(doc, "3.1.1 Class Diagram", 4)
    add_diagram(doc, "r4_fig4_class_lobby.png", "Figure 4.4: Class Diagram for Account & Lobby Management", 6.0)
    doc.add_paragraph("Unity Client – Key Classes:").runs[0].font.bold = True
    unity_classes = [
        ("ApiClient (static class)",
         "Fields: baseUrl: string, authToken: string\n"
         "Methods: SetAuthToken(token), GetAsync<T>(path), PostAsync<T>(path, body), DeleteAsync<T>(path)"),
        ("AuthDTO",
         "Fields: email: string, password: string, username: string"),
        ("LoginResponseDTO",
         "Fields: token: string, role: string, userId: string"),
        ("LobbyDTO",
         "Fields: lobbyCode: string, player1: PlayerSlot, player2: PlayerSlot, status: string"),
        ("PlayerSlot",
         "Fields: playerId: string, username: string, role: string (Dad | Child)"),
    ]
    for name, desc in unity_classes:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(name + "\n")
        r.font.bold = True
        p.add_run(desc)

    doc.add_paragraph()
    doc.add_paragraph("Backend – Key Components:").runs[0].font.bold = True
    backend_classes = [
        ("authRouter (Express Router)",
         "POST /login → authController.login(req, res)\n"
         "POST /register → authController.register(req, res)"),
        ("authController",
         "login(): bcrypt.compare() + jwt.sign({userId, role}, secret, {expiresIn:'24h'})\n"
         "register(): hash password, insert user, return 201"),
        ("lobbyController",
         "Methods: create(), join(), swap(), ready(), getCurrent(), getByCode(), leave(), close()"),
    ]
    for name, desc in backend_classes:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(name + "\n")
        r.font.bold = True
        p.add_run(desc)

    # 3.1.2 Login Sequence
    add_heading(doc, "3.1.2 Login Sequence Diagram", 4)
    add_diagram(doc, "r4_fig5_seq_login.png", "Figure 4.5: Sequence Diagram for User Login and JWT Issuance", 6.0)
    login_steps = [
        "Player → Unity Login Screen: Enter email + password",
        "Unity Login Screen → ApiClient: PostAsync(\"/api/auth/login\", {email, password})",
        "ApiClient → Backend /api/auth/login: HTTP POST with JSON body",
        "Backend: Validate credentials, bcrypt.compare(plain, hash)",
        "Backend: jwt.sign({userId, role}, secret, {expiresIn:'24h'})",
        "Backend → ApiClient: 200 OK {token, role, userId}",
        "ApiClient → Unity Login Screen: Store token in PlayerPrefs",
        "Unity Login Screen → Lobby Browser: Navigate()",
    ]
    for i, step in enumerate(login_steps, 1):
        p = doc.add_paragraph(style='List Number')
        p.add_run(step)

    # 3.1.3 Create & Join Lobby Sequence
    add_heading(doc, "3.1.3 Create & Join Lobby Sequence", 4)
    add_diagram(doc, "r4_fig6_seq_lobby.png", "Figure 4.6: Sequence Diagram for 2-Player Matchmaking and Role Assignment", 6.0)
    lobby_steps = [
        "Player A → POST /api/game/lobby/create → Backend generates 6-char alphanumeric code → Returns {lobbyCode}",
        "Player A shares code with Player B (out-of-band, e.g., voice or Discord)",
        "Player B → POST /api/game/lobby/join {lobbyCode} → Backend checks slot availability → Assigns Child role → Returns full lobby state",
        "Both clients → Poll GET /api/game/lobby/:code every 2 seconds → Update Lobby3D UI",
        "Both players click Ready → POST /api/game/lobby/:code/ready → When both ready → Status = READY → Navigate to Game Scene",
    ]
    for step in lobby_steps:
        p = doc.add_paragraph(style='List Number')
        p.add_run(step)

    # ── 3.2 Voice Chat System ────────────────────────────────────────────
    add_heading(doc, "3.2 Voice Chat System (FE-07 / VOICE-001–004)", 3)

    # 3.2.1 Class Diagram
    add_heading(doc, "3.2.1 Class Diagram", 4)
    add_diagram(doc, "r4_fig7_class_voice.png", "Figure 4.7: Class Diagram for Voice Chat Streaming & DSP Pitch Shifter", 6.0)

    doc.add_paragraph("VoiceChatManager (MonoBehaviour):").runs[0].font.bold = True
    add_code_block(doc,
        "Fields:\n"
        "  _ws           : ClientWebSocket\n"
        "  _mic          : AudioClip\n"
        "  _jitterBuffer : Queue<byte[]>\n"
        "  _isPTT        : bool\n"
        "  _isAutoGate   : bool\n"
        "  _isConnected  : bool\n"
        "\n"
        "Methods:\n"
        "  ConnectAsync(lobbyId, lobbyCode, playerId, token) : Task\n"
        "  StartMicrophone()   : void\n"
        "  SendAudioFrame()    : void\n"
        "  ReceiveLoop()       : Task\n"
        "  ProcessJitterBuffer(): void\n"
        "  OnVKeyDown()        : void    // PTT key handler\n"
        "  Disconnect()        : void"
    )

    doc.add_paragraph()
    doc.add_paragraph("VoiceDSPProcessor (static class):").runs[0].font.bold = True
    add_code_block(doc,
        "Constants:\n"
        "  DAD_PITCH_SHIFT   = -4f   // semitones\n"
        "  CHILD_PITCH_SHIFT = +5f   // semitones\n"
        "\n"
        "Methods:\n"
        "  ShiftPitch(samples: float[], semitones: float) : float[]\n"
        "  ApplyFormant(samples: float[], shift: float)  : float[]"
    )

    doc.add_paragraph()
    doc.add_paragraph("Backend – voiceRelay.js (Node module):").runs[0].font.bold = True
    add_code_block(doc,
        "Internal state:\n"
        "  rooms : Map<lobbyId, Set<WebSocket>>\n"
        "\n"
        "Functions:\n"
        "  attachVoiceRelay(server) : void   // registers WS upgrade handler\n"
        "  authenticate(ws, frame)  : bool\n"
        "  relay(ws, frame, lobbyId): void"
    )

    # 3.2.2 Voice Chat Connection Sequence
    add_heading(doc, "3.2.2 Voice Chat Connection Sequence Diagram", 4)
    add_diagram(doc, "r4_fig8_seq_voice.png", "Figure 4.8: Sequence Diagram for Voice Chat Handshake & Binary Audio Streaming", 6.0)
    voice_steps = [
        "VoiceChatManager.ConnectAsync() called after lobby is successfully joined",
        "Client opens WSS connection to wss://hold-my-hand-backend.vercel.app/voice",
        "Client sends text frame: {type:\"auth\", lobbyId, lobbyCode, playerId, token}",
        "Backend verifies RS256 JWT via Unity JWKS endpoint",
        "Backend calls Unity Lobby GET /v1/{lobbyId} to verify player membership",
        "Backend sends {type:\"ready\", playerId, lobbyCode} on success (or closes with code 1008 on failure)",
        "VoiceChatManager.StartMicrophone() — Microphone.Start() at 16 kHz, 1 channel",
        "On PTT V key press (or AutoGate voice activity detection): SendAudioFrame() triggered",
        "SendAudioFrame(): Capture 320 PCM float samples → Convert to 16-bit → Prepend 4-byte sequence number → Send binary frame (644 bytes total)",
        "Server relays binary frame to the other player in the same lobbyId room",
        "ReceiveLoop(): Process incoming binary frames → Deserialize PCM → Enqueue to _jitterBuffer",
        "ProcessJitterBuffer(): VoiceDSPProcessor.ShiftPitch() → AudioSource.PlayOneShot() on audio thread",
    ]
    for step in voice_steps:
        p = doc.add_paragraph(style='List Number')
        p.add_run(step)

    # 3.2.3 DSP Processing Design
    add_heading(doc, "3.2.3 DSP Processing Design", 4)
    doc.add_paragraph(
        "The Voice DSP pipeline processes raw microphone audio on the sending side "
        "before transmission, so both the sender and the receiver hear the processed voice."
    )
    dsp_table_headers = ["Stage", "Input", "Output", "Notes"]
    dsp_table_rows = [
        ("1. Capture",        "Microphone.GetData()",       "Raw PCM float[]",           "320 samples @ 16 kHz"),
        ("2. Normalize",      "Raw PCM float[]",            "Normalized float[]",         "Peak normalization to ±0.95"),
        ("3. Pitch Shift",    "Normalized float[]",         "Pitch-shifted float[]",      "Dad: -4 st | Child: +5 st (phase vocoder approx.)"),
        ("4. Formant Adjust", "Pitch-shifted float[]",      "Formant-adjusted float[]",   "Envelope spectral shaping for naturalness"),
        ("5. Pack & Send",    "Formant-adjusted float[]",   "binary WebSocket frame",     "Convert to Int16 PCM, prepend 4-byte seq, send"),
    ]
    add_table(doc, dsp_table_headers, dsp_table_rows)
    doc.add_paragraph()

    dsp_details = [
        ("Dad role (-4 semitones):",  "Deepens and broadens voice to sound more \"fatherly\"."),
        ("Child role (+5 semitones):", "Raises and narrows voice to sound more \"childlike\"."),
        ("Applied on sending side:",   "Both sender and receiver experience the same processed audio, ensuring consistent character voice during gameplay."),
    ]
    for title, detail in dsp_details:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(title + " ")
        r.font.bold = True
        p.add_run(detail)

    # ── 3.3 Moderator Force Close Lobby ─────────────────────────────────
    add_heading(doc, "3.3 Moderator Force Close Lobby (MOD-001)", 3)

    # 3.3.1 Class Diagram
    add_heading(doc, "3.3.1 Class Diagram", 4)

    doc.add_paragraph("Unity Client – Moderator Classes:").runs[0].font.bold = True
    add_code_block(doc,
        "ModApiClient (static class, extends ApiClient)\n"
        "  Methods:\n"
        "    CloseLobbyAsync(lobbyCode: string, req: CloseLobbyRequest) : Task<CloseLobbyResponse>\n"
        "\n"
        "CloseLobbyRequest\n"
        "  Fields: reason: string, notifyPlayers: bool\n"
        "\n"
        "CloseLobbyResponse\n"
        "  Fields: success: bool, lobbyCode: string, previousStatus: string,\n"
        "          currentStatus: string, closedAt: string, reason: string"
    )

    doc.add_paragraph()
    doc.add_paragraph("Backend – Moderator Components:").runs[0].font.bold = True
    add_code_block(doc,
        "modRouter (Express Router)\n"
        "  POST /lobby/:code/close → modController.closeLobby(req, res)\n"
        "\n"
        "modController.closeLobby(req, res)\n"
        "  1. middleware/auth.js → authenticate() → authorize('Moderator')\n"
        "  2. lobbyStore.get(code) → validate lobby exists\n"
        "  3. Validate status !== 'CLOSED'\n"
        "  4. lobbyStore.update(code, {status:'CLOSED', closedBy, closedAt, reason})\n"
        "  5. auditLogs.insertOne({actorId, action:'CLOSE_LOBBY', targetId:code, timestamp})\n"
        "  6. Return 200 OK with CloseLobbyResponse"
    )

    # 3.3.2 Force Close Sequence
    add_heading(doc, "3.3.2 Force Close Lobby Sequence Diagram", 4)
    add_diagram(doc, "r4_fig9_seq_mod_close.png", "Figure 4.9: Sequence Diagram for Moderator Force Close Lobby (MOD-001)", 6.0)
    mod_steps = [
        "Moderator clicks \"Force Close\" on Web Portal Admin Panel",
        "Web Portal → POST /api/mod/lobby/:code/close  { reason, notifyPlayers: true }",
        "Backend middleware/auth.js → authenticate() → authorize('Moderator')",
        "Backend: lobbyStore.get(code) → Validate lobby exists and is not already CLOSED",
        "Backend: lobbyStore.update(code, { status: 'CLOSED', closedBy, closedAt, reason })",
        "Backend: auditLogs.insertOne({ actorId, action: 'CLOSE_LOBBY', targetId: code, details, timestamp })",
        "Backend → Web Portal: 200 OK { success: true, lobbyCode, previousStatus: 'WAITING', currentStatus: 'CLOSED', affectedPlayers: [] }",
        "[BLOCKED] Backend notifies Unity clients via UGS Lobby webhook/polling — pending UGS integration",
    ]
    for step in mod_steps:
        p = doc.add_paragraph(style='List Number')
        if step.startswith("[BLOCKED]"):
            p.add_run(step).font.color.rgb = RGBColor(0xC0, 0x00, 0x00)
        else:
            p.add_run(step)

    p_blocked = doc.add_paragraph()
    r_b = p_blocked.add_run("Note (BLOCKED): ")
    r_b.font.bold = True
    r_b.font.color.rgb = DARK_RED
    p_blocked.add_run(
        "Step 8 (real-time Unity client notification) is currently blocked pending "
        "UGS Lobby webhook integration. The backend state update (steps 4–7) is fully "
        "operational. Unity clients will detect the closed lobby on the next polling cycle."
    )

    # ── 3.4 Text Chat in Lobby ───────────────────────────────────────────
    add_heading(doc, "3.4 Text Chat in Lobby (CHAT-001)", 3)

    # 3.4.1 Class Diagram
    add_heading(doc, "3.4.1 Class Diagram", 4)
    doc.add_paragraph(
        "Text Chat is implemented using the Unity Lobby player data property bus. "
        "No dedicated backend endpoint is required; messages are piggybacked on "
        "lobby state polling."
    )
    add_code_block(doc,
        "ChatMessage\n"
        "  Fields: senderId: string, senderName: string, content: string, timestamp: long\n"
        "\n"
        "Lobby3DManager (MonoBehaviour)  [Blocks.Sessions assembly]\n"
        "  Fields:\n"
        "    _chatMessages : List<ChatMessage>\n"
        "    _chatInput    : TextField\n"
        "  Methods:\n"
        "    SendChatMessage()    : void   // Writes LAST_MSG to UGS playerData\n"
        "    ReceiveChatMessage() : void   // Reads LAST_MSG from polled lobby state\n"
        "    UpdateChatUI()       : void   // Refreshes UI chat list\n"
        "\n"
        "Implementation Note:\n"
        "  Unity Lobby custom playerData property key: 'LAST_MSG'\n"
        "  Poll interval: 1 second\n"
        "  Status: REVIEW (pending acceptance testing)"
    )

    chat_design_points = [
        ("Message Bus:", "Unity Lobby playerData custom property LAST_MSG used as an ephemeral message relay."),
        ("Poll Interval:", "1-second polling interval via Lobby SDK HeartbeatAsync / QueryLobbies."),
        ("Persistence:", "Messages are NOT persisted in the database; chat history is session-only."),
        ("Status:", "CHAT-001 is currently in REVIEW state awaiting QA acceptance testing."),
    ]
    doc.add_paragraph()
    for title, detail in chat_design_points:
        p = doc.add_paragraph(style='List Bullet')
        r = p.add_run(title + " ")
        r.font.bold = True
        p.add_run(detail)

    # ── 3.5 Lobby & Game Session State Machine Diagram ──────────────────
    add_heading(doc, "3.5 Lobby & Game Session State Machine Diagram (STATE-001)", 3)
    add_diagram(doc, "r4_fig10_state_machine.png", "Figure 4.10: UML State Machine Diagram for Lobby & Game Session Lifecycle", 6.2)
    doc.add_paragraph(
        "Figure 4.10 specifies the finite state automaton governing the entire lifecycle of a multiplayer co-op match in Hold My Hand. "
        "Transitions between states (WAITING_FOR_PEER, PEER_CONNECTED, READY_TO_START, LOADING_STAGE, IN_SESSION composite state, "
        "VICTORY_SUMMARY, DEFEAT_SUMMARY, and TERMINATED_CLOSED) are guarded by deterministic event triggers and RBAC permissions."
    )

    # ─── Final paragraph ─────────────────────────────────────────────────────
    doc.add_paragraph()
    doc.add_paragraph()
    p_end = doc.add_paragraph()
    p_end.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_end = p_end.add_run("– End of Document –")
    r_end.font.italic = True
    r_end.font.color.rgb = RGBColor(0x60, 0x60, 0x60)

    # ─── Save ────────────────────────────────────────────────────────────────
    doc.save(OUTPUT_PATH)
    print(f"[OK] Saved: {OUTPUT_PATH}")


if __name__ == "__main__":
    build_document()
