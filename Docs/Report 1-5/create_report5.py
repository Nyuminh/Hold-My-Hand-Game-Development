# -*- coding: utf-8 -*-
"""
Script tạo FA26SE047_Report5_TestDoc.docx
Report 5 – Software Test Documentation
Project: Hold My Hand (FA26SE047)
"""

from docx import Document
from docx.shared import Pt, RGBColor, Cm, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import copy

# ── Color constants ──────────────────────────────────────────────────────────
DARK_RED  = RGBColor(0xC0, 0x00, 0x00)
GREEN     = RGBColor(0x00, 0xB0, 0x50)
BLUE      = RGBColor(0x2E, 0x75, 0xB5)
BLACK     = RGBColor(0x00, 0x00, 0x00)
WHITE     = RGBColor(0xFF, 0xFF, 0xFF)
HEADER_BG = "2E75B5"   # table header background (hex, no #)
LIGHT_BG  = "D9E2F3"   # alternating row (optional)

# ── Helpers ───────────────────────────────────────────────────────────────────

def set_cell_bg(cell, hex_color: str):
    """Fill a table cell background with a hex colour string (e.g. '2E75B5')."""
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_color)
    tcPr.append(shd)


def set_cell_borders(table):
    """Add thin borders to every cell in a table."""
    for row in table.rows:
        for cell in row.cells:
            tc = cell._tc
            tcPr = tc.get_or_add_tcPr()
            tcBorders = OxmlElement("w:tcBorders")
            for side in ("top", "left", "bottom", "right", "insideH", "insideV"):
                border = OxmlElement(f"w:{side}")
                border.set(qn("w:val"), "single")
                border.set(qn("w:sz"), "4")
                border.set(qn("w:space"), "0")
                border.set(qn("w:color"), "000000")
                tcBorders.append(border)
            tcPr.append(tcBorders)



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
    p = doc.add_heading(text, level=level)
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    return p


def add_paragraph(doc, text="", bold=False, italic=False,
                  color=None, size=None, align=WD_ALIGN_PARAGRAPH.LEFT):
    p = doc.add_paragraph()
    p.alignment = align
    run = p.add_run(text)
    run.bold = bold
    run.italic = italic
    if color:
        run.font.color.rgb = color
    if size:
        run.font.size = Pt(size)
    return p


def style_status_run(run, status: str):
    s = status.strip().upper()
    if s == "PASS" or s == "DONE":
        run.font.color.rgb = GREEN
        run.bold = True
    elif s in ("BLOCKED", "FAIL", "FAILED"):
        run.font.color.rgb = DARK_RED
        run.bold = True
    elif s in ("PLANNED", "REVIEW", "IN PROGRESS"):
        run.font.color.rgb = BLUE
        run.bold = True


def add_table(doc, headers, rows, col_widths=None, status_cols=None):
    """
    headers : list of str
    rows    : list of list of str
    col_widths : list of Cm/Inches values (optional)
    status_cols : set of column indices that should be coloured by status
    """
    if status_cols is None:
        status_cols = set()

    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.LEFT

    # Header row
    hdr_row = table.rows[0]
    for i, hdr in enumerate(headers):
        cell = hdr_row.cells[i]
        cell.text = ""
        run = cell.paragraphs[0].add_run(hdr)
        run.bold = True
        run.font.color.rgb = WHITE
        run.font.size = Pt(10)
        cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
        set_cell_bg(cell, HEADER_BG)

    # Data rows
    for r_idx, row_data in enumerate(rows):
        row = table.rows[r_idx + 1]
        for c_idx, cell_text in enumerate(row_data):
            cell = row.cells[c_idx]
            cell.text = ""
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            run = p.add_run(str(cell_text))
            run.font.size = Pt(9.5)
            if c_idx in status_cols:
                style_status_run(run, str(cell_text))

    set_cell_borders(table)

    # Column widths
    if col_widths:
        for i, width in enumerate(col_widths):
            for row in table.rows:
                row.cells[i].width = width

    doc.add_paragraph()  # spacing after table
    return table


def page_break(doc):
    doc.add_page_break()


# ═══════════════════════════════════════════════════════════════════════════════
# BUILD DOCUMENT
# ═══════════════════════════════════════════════════════════════════════════════

doc = Document()

# ── Page margins ──────────────────────────────────────────────────────────────
for section in doc.sections:
    section.top_margin    = Cm(2.5)
    section.bottom_margin = Cm(2.5)
    section.left_margin   = Cm(3.0)
    section.right_margin  = Cm(2.0)

# ═══════════════════════════════════════════════════════════════════════════════
# COVER PAGE
# ═══════════════════════════════════════════════════════════════════════════════

for _ in range(4):
    doc.add_paragraph()

# Cover Logo
logo_path = os.path.join(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams", "fpt_logo.png")
if os.path.exists(logo_path):
    p_logo = doc.add_paragraph()
    p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_logo.paragraph_format.space_after = Pt(10)
    p_logo.add_run().add_picture(logo_path, width=Inches(2.2))

# Title 1
p1 = doc.add_paragraph()
p1.alignment = WD_ALIGN_PARAGRAPH.CENTER
r1 = p1.add_run("CAPSTONE PROJECT REPORT")
r1.bold = True
r1.font.color.rgb = DARK_RED
r1.font.size = Pt(28)

doc.add_paragraph()

# Title 2
p2 = doc.add_paragraph()
p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
r2 = p2.add_run("Report 5 \u2013 Software Test Documentation")
r2.bold = True
r2.font.color.rgb = DARK_RED
r2.font.size = Pt(22)

doc.add_paragraph()

# Group & project info
def cover_line(text, sz=12, bold=False):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run(text)
    r.font.size = Pt(sz)
    r.bold = bold
    return p

cover_line("Project Code: FA26SE047 | Class: Fall 2026", 13, True)
cover_line("Group: GFA26SE047 \u2013 Hold My Hand Team", 12)
doc.add_paragraph()
cover_line("Team Members:", 12, True)
cover_line("\u0110o\u00e0n L\u00fd H\u1ed3ng Minh (SE180733) \u2013 PM", 11)
cover_line("\u0110\u1eb7ng Duy Ph\u00fa (SE180727) \u2013 Backend Dev / Report 5 Lead", 11)
cover_line("Tr\u1ea7n Huy D\u01b0\u01a1ng (SE180648) \u2013 Unity Dev", 11)
cover_line("Nguy\u1ec5n Ph\u00fa L\u00e2m (SE180388) \u2013 Unity Dev", 11)
doc.add_paragraph()
cover_line("Supervisor: Th\u0053. Ph\u1ea1m Thanh Tr\u00ed", 12)

for _ in range(4):
    doc.add_paragraph()

# City & date
p_city = doc.add_paragraph()
p_city.alignment = WD_ALIGN_PARAGRAPH.CENTER
r_city = p_city.add_run("\u2013 Ho Chi Minh City, October 2026 \u2013")
r_city.font.size = Pt(14)

page_break(doc)

# ═══════════════════════════════════════════════════════════════════════════════
# I. RECORD OF CHANGES
# ═══════════════════════════════════════════════════════════════════════════════

add_heading(doc, "I. Record of Changes", 1)

p_note = doc.add_paragraph()
p_note.add_run("A \u2013 Added     M \u2013 Modified     D \u2013 Deleted").italic = True

changes_headers = ["Date", "A/M/D", "In Charge", "Change Description"]
changes_rows = [
    ["15/10/2026", "A", "\u0110\u1eb7ng Duy Ph\u00fa", "Initial draft of Report 5 (Test Documentation v0.9)"],
    ["25/10/2026", "M", "\u0110\u1eb7ng Duy Ph\u00fa", "Add Unit Test results for Backend API (Voice relay, Auth)"],
    ["30/10/2026", "M", "\u0110o\u00e0n L\u00fd H\u1ed3ng Minh", "Add Integration Test cases, update blockers"],
]
add_table(doc, changes_headers, changes_rows,
          col_widths=[Cm(3), Cm(2), Cm(4.5), Cm(9)])

# ═══════════════════════════════════════════════════════════════════════════════
# II. TESTING DOCUMENTATION
# ═══════════════════════════════════════════════════════════════════════════════

add_heading(doc, "II. Testing Documentation", 1)

# ── 1. Scope of Testing ───────────────────────────────────────────────────────
add_heading(doc, "1. Scope of Testing", 2)

add_paragraph(doc, "The testing scope for Hold My Hand (FA26SE047) covers the following functional areas:")

scope_items = [
    "Account & Authentication (FE-01): Register, Login, JWT token issuance and validation.",
    "Lobby Management (FE-01): Create lobby, join lobby, swap role, ready up, leave lobby.",
    "Moderator Controls (FE-06 / MOD-001): Force close lobby via REST API.",
    "Voice Chat System (FE-07 / VOICE-001 to VOICE-005): WebSocket relay, DSP pitch shift, PTT/Auto gate UI, E2E audio delivery.",
    "Unity Game Client (FE-02, FE-03): Character controller initialization, role assignment, scene transitions.",
    "Text Chat (CHAT-001): In-lobby text message send/receive.",
]
for item in scope_items:
    p = doc.add_paragraph(style="List Bullet")
    p.add_run(item).font.size = Pt(11)

add_paragraph(doc, "\nTesting Stages:", bold=True)
stages = [
    "Unit Testing: Individual backend functions (Jest + Supertest) and Unity component methods.",
    "Integration Testing: API endpoint chains (Auth \u2192 Lobby create \u2192 Lobby join), Voice WebSocket auth + relay flow.",
    "System Testing: Full 2-player co-op session from login to game completion.",
    "Acceptance Testing: Validating deliverables against Capstone acceptance criteria.",
]
for s in stages:
    p = doc.add_paragraph(style="List Bullet")
    p.add_run(s).font.size = Pt(11)

add_paragraph(doc, "\nConstraints & Assumptions:", bold=True)
constraints = [
    "Voice Chat E2E (VOICE-005) requires 2 physical machines or 2 concurrent game windows; currently BLOCKED.",
    "MOD-001 Unity-side integration test pending UGS Lobby webhook implementation.",
    "System testing assumes Vercel backend deployment is live and MongoDB/Redis accessible.",
    "Test environment uses local development server (localhost:5181) for Unit and Integration tests.",
]
for c in constraints:
    p = doc.add_paragraph(style="List Bullet")
    p.add_run(c).font.size = Pt(11)

# ── 2. Test Strategy ──────────────────────────────────────────────────────────
add_heading(doc, "2. Test Strategy", 2)
add_diagram(doc, "r5_fig2_test_pipeline.png", "Figure 5.1: Multi-Level Test Strategy and Verification Pipeline", 6.0)

# 2.1 Testing Types
add_heading(doc, "2.1 Testing Types", 3)

types_headers = ["Testing Type", "Description", "Target Features", "Pass Criteria"]
types_rows = [
    ["Unit Testing",
     "Test individual functions/methods in isolation using mocks.",
     "Backend: auth, lobby, mod endpoints; Unity: VoiceDSPProcessor, ApiClient",
     "All assertions pass, no unhandled exceptions"],
    ["Integration Testing",
     "Test API endpoint chains and service interactions end-to-end within the backend.",
     "Auth\u2192Lobby flow, Voice WS auth sequence, Mod close flow",
     "Correct HTTP status codes and response bodies"],
    ["System Testing",
     "Full 2-player session test including Unity client + Backend.",
     "Full game flow: Login \u2192 Lobby \u2192 Game \u2192 Result",
     "No crashes, correct game state transitions"],
    ["Performance Testing",
     "Measure API response times and voice latency under load.",
     "Backend REST endpoints, Voice WebSocket relay",
     "P95 REST < 300ms, Voice latency < 150ms"],
    ["Regression Testing",
     "Re-run all previous tests after each code change to ensure no breakage.",
     "All DONE features",
     "All previously passing tests continue to pass"],
]
add_table(doc, types_headers, types_rows,
          col_widths=[Cm(3.5), Cm(5), Cm(5), Cm(5)])

# 2.2 Test Levels
add_heading(doc, "2.2 Test Levels", 3)

levels = [
    ("Unit Test Level",
     "Executed per developer during feature development. Tools: Jest (Backend), Unity Test Runner (Unity C#). "
     "Coverage target: \u226570% statement coverage on backend service functions."),
    ("Integration Test Level",
     "Executed after each feature branch merge to `develop`. Tools: Supertest (HTTP), custom WebSocket test client."),
    ("System Test Level",
     "Executed during Sprint Review. Two-machine setup with real Unity builds connecting to deployed Vercel backend."),
    ("Acceptance Test Level",
     "Executed before each Capstone Review submission. Against Capstone acceptance criteria defined in Report 2."),
]
for title, desc in levels:
    p = doc.add_paragraph()
    p.add_run(title + ": ").bold = True
    p.add_run(desc)

doc.add_paragraph()

# 2.3 Supporting Tools
add_heading(doc, "2.3 Supporting Tools", 3)

tools_headers = ["Tool", "Category", "Usage"]
tools_rows = [
    ["Jest", "Unit/Integration Test Framework", "Backend NodeJS API testing"],
    ["Supertest", "HTTP Testing", "REST endpoint assertion"],
    ["Unity Test Runner", "Unit Test Framework", "Unity C# component testing"],
    ["Postman", "Manual API Testing", "Ad-hoc endpoint verification and auth token testing"],
    ["Discord Voice", "Manual Voice Test", "Real-time voice quality subjective evaluation"],
    ["Git (GitHub)", "Version Control", "Commit-based test evidence tracking"],
    ["VS Code", "IDE", "Test script execution and debugging"],
]
add_table(doc, tools_headers, tools_rows,
          col_widths=[Cm(4), Cm(5.5), Cm(9)])

# ── 3. Test Plan ──────────────────────────────────────────────────────────────
add_heading(doc, "3. Test Plan", 2)

# 3.1 Human Resources
add_heading(doc, "3.1 Human Resources", 3)

hr_headers = ["Role", "Name", "Responsibilities"]
hr_rows = [
    ["Test Lead",
     "\u0110\u1eb7ng Duy Ph\u00fa (SE180727)",
     "Design test cases, execute backend unit tests, compile test reports"],
    ["Backend Tester",
     "\u0110o\u00e0n L\u00fd H\u1ed3ng Minh (SE180733)",
     "Execute integration tests, review API test results"],
    ["Unity Tester",
     "Tr\u1ea7n Huy D\u01b0\u01a1ng (SE180648)",
     "Execute Unity component tests, system test (game client)"],
    ["Unity Tester",
     "Nguy\u1ec5n Ph\u00fa L\u00e2m (SE180388)",
     "Execute system tests (2-player setup), voice chat E2E"],
    ["Supervisor Reviewer",
     "Th\u0053. Ph\u1ea1m Thanh Tr\u00ed",
     "Accept/reject test evidence at Review sessions"],
]
add_table(doc, hr_headers, hr_rows,
          col_widths=[Cm(4), Cm(5.5), Cm(9)])

# 3.2 Test Environment
add_heading(doc, "3.2 Test Environment", 3)

env_headers = ["Environment", "Specification", "Usage"]
env_rows = [
    ["Backend (Local Dev)",
     "Node.js 18.x, Express 4, MongoDB local, Redis fallback (in-memory), localhost:5181",
     "Unit & Integration Testing"],
    ["Backend (Production)",
     "Vercel Serverless (Node.js 18), MongoDB Atlas, Vercel KV (Redis), URL: hold-my-hand-backend.vercel.app",
     "System & Acceptance Testing"],
    ["Unity Client",
     "Unity 6 (6000.5.10f1), Windows 11 64-bit, NVIDIA RTX 3050 Ti, RAM 16GB",
     "System Testing"],
    ["Voice Test Machine 1",
     "Windows 11, Headset + Mic, Unity build or Editor play mode",
     "VOICE-005 E2E test"],
    ["Voice Test Machine 2",
     "Windows 11, Headset + Mic, Unity build or Editor play mode",
     "VOICE-005 E2E test"],
    ["Test Browser",
     "Chrome 120+",
     "Web Portal testing (React app)"],
]
add_table(doc, env_headers, env_rows,
          col_widths=[Cm(4), Cm(9), Cm(5.5)])

# 3.3 Test Milestones
add_heading(doc, "3.3 Test Milestones", 3)

ms_headers = ["Milestone", "Target Date", "Description", "Status"]
ms_rows = [
    ["M1: Unit Tests Backend",    "20/10/2026", "All backend API unit tests written and passing",                            "DONE"],
    ["M2: Integration Tests Auth+Lobby", "25/10/2026", "Auth flow + Lobby CRUD integration tests passing",                 "DONE"],
    ["M3: MOD-001 Backend Test",  "27/10/2026", "Force close lobby endpoint test passing",                                  "DONE"],
    ["M4: Voice Unit Tests",      "30/10/2026", "VoiceDSP pitch shift frequency verification pass",                         "DONE"],
    ["M5: System Test (1 machine)", "05/11/2026", "Single-machine dual-window system test (pending)",                      "PLANNED"],
    ["M6: Voice E2E (2 machines)", "07/11/2026", "VOICE-005 two-machine live voice test",                                  "BLOCKED"],
    ["M7: Acceptance Test",       "15/11/2026", "Full acceptance criteria validation before Review 3",                      "PLANNED"],
]
add_table(doc, ms_headers, ms_rows,
          col_widths=[Cm(5), Cm(3), Cm(8), Cm(2.5)],
          status_cols={3})

# ── 4. Test Cases ─────────────────────────────────────────────────────────────
add_heading(doc, "4. Test Cases", 2)

add_paragraph(doc,
    "Below are representative test cases. Complete test case matrices are maintained in:")
p = doc.add_paragraph(style="List Bullet")
p.add_run("Unit Test Cases: Report5_Unit Test.xls")
p = doc.add_paragraph(style="List Bullet")
p.add_run("Integration & System Test Cases: Report5_Test Report.xlsx")
doc.add_paragraph()

# 4.1 Backend Unit Tests
add_heading(doc, "4.1 Backend Unit Tests (Jest + Supertest)", 3)

be_headers = ["TC-ID", "Feature", "Test Case Name", "Pre-conditions",
              "Test Steps", "Expected Result", "Actual Result", "Status"]
be_rows = [
    ["TC-BE-001","Auth","Register new user","DB empty of test email",
     'POST /api/auth/register {username:"testuser", email:"t@t.com", password:"Test1234!"}',
     '200 OK {success:true, message:"User registered"}',
     "200 OK {success:true}","PASS"],
    ["TC-BE-002","Auth","Login valid credentials","User exists",
     'POST /api/auth/login {email:"t@t.com", password:"Test1234!"}',
     "200 OK with JWT token in response",
     '200 OK {token:"eyJ..."}', "PASS"],
    ["TC-BE-003","Auth","Login wrong password","User exists",
     'POST /api/auth/login {email:"t@t.com", password:"Wrong!"}',
     "401 Unauthorized",
     '401 {message:"Invalid email or password"}', "PASS"],
    ["TC-BE-004","Lobby","Create lobby","Authenticated player token",
     "POST /api/game/lobby/create (Bearer token)",
     "200 OK {lobbyCode: 6-char string}",
     '200 OK {lobbyCode:"AB12CD"}', "PASS"],
    ["TC-BE-005","Lobby","Join lobby","2 players authenticated, lobby created",
     'POST /api/game/lobby/join {lobbyCode:"AB12CD"} (Player2 token)',
     "200 OK, player2 assigned Child role",
     '200 OK {player2Role:"Child"}', "PASS"],
    ["TC-BE-006","Lobby","Join full lobby","Lobby has 2 players",
     "POST /api/game/lobby/join (Player3 token)",
     "409 Conflict",
     '409 {message:"Lobby is full"}', "PASS"],
    ["TC-BE-007","Lobby","Swap roles","2 players in WAITING lobby",
     "POST /api/game/lobby/AB12CD/swap (Player1 token)",
     "200 OK, roles swapped",
     '200 OK {player1Role:"Child", player2Role:"Dad"}', "PASS"],
    ["TC-BE-008","Mod","Force close lobby","Moderator token, active lobby",
     'POST /api/mod/lobby/AB12CD/close {reason:"Test", notifyPlayers:true}',
     '200 OK {currentStatus:"CLOSED"}',
     '200 OK {currentStatus:"CLOSED"}', "PASS"],
    ["TC-BE-009","Mod","Force close non-existent lobby","Moderator token",
     "POST /api/mod/lobby/XXXXXX/close",
     "404 Not Found",
     '404 {message:"Lobby not found"}', "PASS"],
    ["TC-BE-010","Mod","Force close with Player token","Player token",
     "POST /api/mod/lobby/AB12CD/close",
     "403 Forbidden",
     '403 {message:"Access denied"}', "PASS"],
]
add_table(doc, be_headers, be_rows,
          col_widths=[Cm(1.8), Cm(1.5), Cm(3), Cm(3), Cm(4), Cm(3.5), Cm(3), Cm(1.5)],
          status_cols={7})

# 4.2 Voice Chat Unit Tests
add_heading(doc, "4.2 Voice Chat Unit Tests (VOICE-001 to VOICE-004)", 3)

voice_headers = ["TC-ID", "Feature", "Test Case Name", "Pre-conditions",
                 "Test Steps", "Expected Result", "Actual Result", "Status"]
voice_rows = [
    ["TC-VOICE-001","VOICE-001","Backend voice relay WS connection",
     "Backend running, test WS client",
     "Connect to ws://localhost:5181/voice, send auth frame",
     'Connection accepted, {type:"ready"} received',
     '{type:"ready"} received', "PASS"],
    ["TC-VOICE-002","VOICE-001","Voice relay between 2 clients",
     "2 WS clients in same lobby",
     "Client A sends 644-byte audio frame",
     "Client B receives identical frame",
     "Frame received by B (verified byte equality)", "PASS"],
    ["TC-VOICE-003","VOICE-002","Dad pitch shift frequency",
     "None",
     "Run VoiceDSPProcessor.ShiftPitch(440Hz, -4 semitones)",
     "Output fundamental frequency \u2248 349.2 Hz (E4)",
     "349.1 Hz (measured via FFT)", "PASS"],
    ["TC-VOICE-004","VOICE-002","Child pitch shift frequency",
     "None",
     "Run VoiceDSPProcessor.ShiftPitch(440Hz, +5 semitones)",
     "Output fundamental frequency \u2248 587.3 Hz (D5)",
     "587.0 Hz (measured via FFT)", "PASS"],
    ["TC-VOICE-005","VOICE-003","PTT key V activation",
     "Unity in play mode, mic connected",
     "Press V key \u2192 hold \u2192 release",
     "VoiceChatManager._isPTT = true while held, audio frames sent while held",
     "Confirmed via Debug.Log", "PASS"],
    ["TC-VOICE-006","VOICE-004","Mic button UI visible in Lobby 3D",
     "Lobby 3D scene loaded",
     "Open Lobby3DScreen.uxml",
     "Microphone button with correct USS styling visible",
     "Button rendered correctly", "PASS"],
    ["TC-VOICE-007","VOICE-005","E2E 2-client voice test",
     "2 machines, deployed backend",
     "Machine A speaks \u2192 Machine B hears",
     "Machine B hears pitch-shifted voice of Machine A",
     "BLOCKED", "BLOCKED"],
]
add_table(doc, voice_headers, voice_rows,
          col_widths=[Cm(2), Cm(1.8), Cm(3), Cm(2.5), Cm(3.5), Cm(3), Cm(2.5), Cm(1.5)],
          status_cols={7})

# 4.3 Unity Component Tests
add_heading(doc, "4.3 Unity Component Tests", 3)

unity_headers = ["TC-ID", "Feature", "Test Case Name", "Pre-conditions",
                 "Test Steps", "Expected Result", "Actual Result", "Status"]
unity_rows = [
    ["TC-UNITY-001","FE-02","Dad character controller init",
     "Unity scene loaded",
     "Instantiate Dad prefab \u2192 check Collider radius",
     "CapsuleCollider.radius = 0.6f",
     "0.6f confirmed", "PASS"],
    ["TC-UNITY-002","FE-02","Child character controller init",
     "Unity scene loaded",
     "Instantiate Child prefab \u2192 check Collider radius",
     "CapsuleCollider.radius = 0.3f",
     "0.3f confirmed", "PASS"],
    ["TC-UNITY-003","FE-01","ApiClient JWT storage",
     "Login successful",
     'Check PlayerPrefs.GetString("jwt_token")',
     "Non-empty string matching returned JWT",
     "Token stored correctly", "PASS"],
    ["TC-UNITY-004","FE-01","ModApiClient CloseLobbyAsync",
     "Moderator JWT, active lobby",
     'Call ModApiClient.CloseLobbyAsync("AB12CD", req)',
     "Returns CloseLobbyResponse with success=true",
     "success=true", "PASS"],
]
add_table(doc, unity_headers, unity_rows,
          col_widths=[Cm(2), Cm(1.5), Cm(3.5), Cm(2.5), Cm(4), Cm(3), Cm(2.5), Cm(1.5)],
          status_cols={7})

# ── 5. Test Reports ───────────────────────────────────────────────────────────
add_heading(doc, "5. Test Reports", 2)
add_diagram(doc, "r5_fig1_test_results.png", "Figure 5.2: Test Execution Results and Pass Rate Breakdown (Report 5)", 6.0)

# 5.1 Unit Test Summary
add_heading(doc, "5.1 Unit Test Summary", 3)

summary_headers = ["Category", "Total TCs", "Passed", "Failed", "Blocked", "Pass Rate"]
summary_rows = [
    ["Backend API (Auth + Lobby + Mod)", "10", "10", "0", "0", "100%"],
    ["Voice Chat (VOICE-001 to VOICE-004)", "6", "6", "0", "0", "100%"],
    ["Voice Chat E2E (VOICE-005)", "1", "0", "0", "1", "BLOCKED"],
    ["Unity Component Tests", "4", "4", "0", "0", "100%"],
    ["TOTAL", "21", "20", "0", "1", "95.2%"],
]
tbl_summary = add_table(doc, summary_headers, summary_rows,
                        col_widths=[Cm(7), Cm(2.5), Cm(2), Cm(2), Cm(2.5), Cm(2.5)],
                        status_cols={5})

# Bold TOTAL row
last_row = tbl_summary.rows[-1]
for cell in last_row.cells:
    for para in cell.paragraphs:
        for run in para.runs:
            run.bold = True

# 5.2 Defect Log
add_heading(doc, "5.2 Defect Log", 3)

defect_headers = ["Defect ID", "Found In", "Description", "Severity", "Status", "Resolution"]
defect_rows = [
    ["DEF-001","TC-BE-004",
     "Lobby code not validated for uniqueness under concurrent creation",
     "Medium","DONE","Added collision retry loop in lobbyStore.create()"],
    ["DEF-002","TC-VOICE-003",
     "Pitch shift approximation error >5% at extreme frequencies (>2kHz)",
     "Low","DONE","Phase vocoder limitation; acceptable for voice range (85Hz\u2013255Hz)"],
    ["DEF-003","VOICE-005",
     "Vercel multi-instance WS room isolation (same lobby, different function instances)",
     "High","BLOCKED","Requires Upstash Redis pub/sub for production delivery; noted in API_CONTRACT.md"],
]
add_table(doc, defect_headers, defect_rows,
          col_widths=[Cm(2.5), Cm(2.5), Cm(5), Cm(2), Cm(2), Cm(4.5)],
          status_cols={4})

# 5.3 Test Analysis
add_heading(doc, "5.3 Test Analysis", 3)

add_paragraph(doc, "Key findings:", bold=True)

findings = [
    "Backend unit tests (Auth, Lobby CRUD, Mod close) achieve 100% pass rate with correct HTTP status codes and response bodies as per API_CONTRACT.md.",
    "VoiceDSP pitch shift (VOICE-002) verified via FFT frequency analysis: Dad voice at -4 semitones produces ~349Hz (within \u00b10.3% of target), Child at +5 semitones produces ~587Hz (within \u00b10.1% of target).",
    "WebSocket relay (VOICE-001) confirmed functional on local backend: 2-client audio frame relay latency < 5ms on localhost.",
    "Critical blocker: VOICE-005 E2E test on deployed Vercel backend cannot be performed with single-machine setup. Multi-instance Redis pub/sub required for production audio delivery between clients on different Vercel function instances (DEF-003).",
    "MOD-001 backend endpoint fully tested and passing. Unity-side integration (UGS Lobby notification) remains in BLOCKED state pending implementation.",
    "All implemented features (VOICE-001 to VOICE-004, MOD-001 backend) are code-complete with commit evidence on GitHub repository FA26SE047-HoldMyHand.",
]
for i, f in enumerate(findings, 1):
    p = doc.add_paragraph(style="List Number")
    p.add_run(f).font.size = Pt(11)

# ═══════════════════════════════════════════════════════════════════════════════
# SAVE
# ═══════════════════════════════════════════════════════════════════════════════

output_path = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\FA26SE047_Report5_TestDoc.docx"
doc.save(output_path)
print(f"[OK] Saved: {output_path}")
