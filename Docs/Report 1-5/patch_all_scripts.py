# -*- coding: utf-8 -*-
"""
Script to embed diagrams and logos into Report 3, Report 4, and Report 5.
Reads create_report3.py, create_report4.py, create_report5.py,
modifies them to include add_picture and add_cover_logo calls,
and executes them to rebuild all .docx files.
"""
import os
import sys

BASE_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5"
DIAG_DIR = os.path.join(BASE_DIR, "diagrams")

# 1. Update create_report3.py
def patch_report3():
    path = os.path.join(BASE_DIR, "create_report3.py")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Check if already patched
    if "r3_fig1_context_diagram.png" in content:
        print("create_report3.py already patched.")
        return

    # Add helper imports and add_diagram function at top
    helper_code = """
def add_diagram(doc, img_name, caption, width_inches=6.0):
    img_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", img_name)
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
"""
    content = content.replace("def add_paragraph(", helper_code + "\ndef add_paragraph(")

    # Cover page logo
    cover_target = '    # University / Organization line\n    p = doc.add_paragraph()\n    p.alignment = WD_ALIGN_PARAGRAPH.CENTER\n    run = p.add_run("FPT UNIVERSITY – SOFTWARE ENGINEERING CAPSTONE")'
    cover_repl = """    # Logo & University line
    logo_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", "fpt_logo.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(10)
        p_logo.add_run().add_picture(logo_path, width=Inches(2.2))

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run("FPT UNIVERSITY – SOFTWARE ENGINEERING CAPSTONE")"""
    content = content.replace(cover_target, cover_repl)

    # 1. Context Diagram
    ctx_target = '    r2 = p.add_run(\n        " [System Context – Unity Client ↔ Backend REST API ↔ MongoDB/Redis | "\n        "Unity Client ↔ Vercel Voice WebSocket | Web Portal ↔ Backend REST API | "\n        "Admin Dashboard ↔ Backend Admin Endpoints]"\n    )'
    ctx_repl = ctx_target + '\n    add_diagram(doc, "r3_fig1_context_diagram.png", "Figure 3.1: Hold My Hand System Context Diagram", 6.0)'
    content = content.replace(ctx_target, ctx_repl)

    # 2. Use Case Diagram
    uc_target = '    # 2.2.1 Diagrams\n    add_heading4(doc, "2.2.1 Diagram(s)")'
    uc_repl = uc_target + '\n    add_diagram(doc, "r3_fig2_usecase_diagram.png", "Figure 3.2: Hold My Hand Overall System Use Case Diagram", 6.2)'
    content = content.replace(uc_target, uc_repl)

    # 3. Screens Flow Diagram
    flow_target = '    for flow in screen_flows:\n        add_bullet(doc, flow)'
    flow_repl = flow_target + '\n    add_diagram(doc, "r3_fig3_screen_flow.png", "Figure 3.3: Hold My Hand Screens Navigation Flow (Game Client & Web Portal)", 6.2)'
    content = content.replace(flow_target, flow_repl)

    # 4. ERD Diagram
    erd_target = '    # 3.1.5\n    add_heading4(doc, "3.1.5 Entity Relationship Diagram")'
    erd_repl = erd_target + '\n    add_diagram(doc, "r3_fig4_erd.png", "Figure 3.4: Hold My Hand Database Entity Relationship Diagram (ERD & Document Schema)", 6.0)'
    content = content.replace(erd_target, erd_repl)

    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("Patched create_report3.py")

# 2. Update create_report4.py
def patch_report4():
    path = os.path.join(BASE_DIR, "create_report4.py")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    if "r4_fig1_system_architecture.png" in content:
        print("create_report4.py already patched.")
        return

    helper_code = """
import os

def add_diagram(doc, img_name, caption, width_inches=6.0):
    img_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", img_name)
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
"""
    content = content.replace("def add_heading(", helper_code + "\ndef add_heading(")

    # Cover page logo
    cover_target = '    # ── Cover line ────────────────────────────────────────────────────────────\n    add_cover_line(doc, "CAPSTONE PROJECT REPORT", 28, bold=True, color=DARK_RED)'
    cover_repl = """    # ── Cover line ────────────────────────────────────────────────────────────
    logo_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", "fpt_logo.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(10)
        p_logo.add_run().add_picture(logo_path, width=Inches(2.2))
    add_cover_line(doc, "CAPSTONE PROJECT REPORT", 28, bold=True, color=DARK_RED)"""
    content = content.replace(cover_target, cover_repl)

    # 1. Architecture diagram
    arch_target = '    # Architecture Diagram (text representation)\n    doc.add_paragraph("Architecture Diagram:").runs[0].font.bold = True'
    arch_repl = '    # Architecture Diagram\n    add_diagram(doc, "r4_fig1_system_architecture.png", "Figure 4.1: Hold My Hand Multi-Tier Distributed Software Architecture", 6.2)\n    doc.add_paragraph("Architecture Overview:").runs[0].font.bold = True'
    content = content.replace(arch_target, arch_repl)

    # 2. Package diagram
    pkg_target = '    add_table(doc,\n        ["Package / Module", "Sub-system", "Responsibility"],\n        pkg_table_rows,\n        header_bg="1F497D"\n    )'
    pkg_repl = '    add_diagram(doc, "r4_fig2_package_diagram.png", "Figure 4.2: Hold My Hand Software Package Structure Diagram", 6.0)\n' + pkg_target
    content = content.replace(pkg_target, pkg_repl)

    # 3. DB Schema / ERD
    db_target = '    add_table(doc,\n        ["Field", "Type", "Description"],\n        redis_rows,\n        header_bg="366092"\n    )'
    db_repl = db_target + '\n    add_diagram(doc, "r4_fig3_db_schema.png", "Figure 4.3: Hold My Hand Database Schema & Redis Cache Structure Diagram", 6.0)'
    content = content.replace(db_target, db_repl)

    # 4. Class diagram Lobby
    cls_lobby_target = '    # 3.1.1 Class Diagram\n    add_heading(doc, "3.1.1 Class Diagram", 4)'
    cls_lobby_repl = cls_lobby_target + '\n    add_diagram(doc, "r4_fig4_class_lobby.png", "Figure 4.4: Class Diagram for Account & Lobby Management", 6.0)'
    content = content.replace(cls_lobby_target, cls_lobby_repl)

    # 5. Login Sequence
    login_target = '    # 3.1.2 Login Sequence\n    add_heading(doc, "3.1.2 Login Sequence Diagram", 4)'
    login_repl = login_target + '\n    add_diagram(doc, "r4_fig5_seq_login.png", "Figure 4.5: Sequence Diagram for User Login and JWT Issuance", 6.0)'
    content = content.replace(login_target, login_repl)

    # 6. Lobby Sequence
    lobby_target = '    # 3.1.3 Create & Join Lobby Sequence\n    add_heading(doc, "3.1.3 Create & Join Lobby Sequence", 4)'
    lobby_repl = lobby_target + '\n    add_diagram(doc, "r4_fig6_seq_lobby.png", "Figure 4.6: Sequence Diagram for 2-Player Matchmaking and Role Assignment", 6.0)'
    content = content.replace(lobby_target, lobby_repl)

    # 7. Class diagram Voice
    cls_voice_target = '    # 3.2.1 Class Diagram\n    add_heading(doc, "3.2.1 Class Diagram", 4)'
    cls_voice_repl = cls_voice_target + '\n    add_diagram(doc, "r4_fig7_class_voice.png", "Figure 4.7: Class Diagram for Voice Chat Streaming & DSP Pitch Shifter", 6.0)'
    content = content.replace(cls_voice_target, cls_voice_repl)

    # 8. Voice Sequence
    voice_seq_target = '    # 3.2.2 Voice Chat Connection Sequence\n    add_heading(doc, "3.2.2 Voice Chat Connection Sequence Diagram", 4)'
    voice_seq_repl = voice_seq_target + '\n    add_diagram(doc, "r4_fig8_seq_voice.png", "Figure 4.8: Sequence Diagram for Voice Chat Handshake & Binary Audio Streaming", 6.0)'
    content = content.replace(voice_seq_target, voice_seq_repl)

    # 9. Force Close Sequence
    mod_seq_target = '    # 3.3.2 Force Close Sequence\n    add_heading(doc, "3.3.2 Force Close Lobby Sequence Diagram", 4)'
    mod_seq_repl = mod_seq_target + '\n    add_diagram(doc, "r4_fig9_seq_mod_close.png", "Figure 4.9: Sequence Diagram for Moderator Force Close Lobby (MOD-001)", 6.0)'
    content = content.replace(mod_seq_target, mod_seq_repl)

    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("Patched create_report4.py")

# 3. Update create_report5.py
def patch_report5():
    path = os.path.join(BASE_DIR, "create_report5.py")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    if "r5_fig1_test_results.png" in content:
        print("create_report5.py already patched.")
        return

    helper_code = """
import os

def add_diagram(doc, img_name, caption, width_inches=6.0):
    img_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", img_name)
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
"""
    content = content.replace("def add_heading(", helper_code + "\ndef add_heading(")

    # Cover page logo
    cover_target = '# Title 1\np1 = doc.add_paragraph()\np1.alignment = WD_ALIGN_PARAGRAPH.CENTER\nr1 = p1.add_run("CAPSTONE PROJECT REPORT")'
    cover_repl = """# Cover Logo
logo_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", "fpt_logo.png")
if os.path.exists(logo_path):
    p_logo = doc.add_paragraph()
    p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_logo.paragraph_format.space_after = Pt(10)
    p_logo.add_run().add_picture(logo_path, width=Inches(2.2))

# Title 1
p1 = doc.add_paragraph()
p1.alignment = WD_ALIGN_PARAGRAPH.CENTER
r1 = p1.add_run("CAPSTONE PROJECT REPORT")"""
    content = content.replace(cover_target, cover_repl)

    # Test Strategy pipeline diagram
    strat_target = '# ── 2. Test Strategy ──────────────────────────────────────────────────────────\nadd_heading(doc, "2. Test Strategy", 2)'
    strat_repl = strat_target + '\nadd_diagram(doc, "r5_fig2_test_pipeline.png", "Figure 5.1: Multi-Level Test Strategy and Verification Pipeline", 6.0)'
    content = content.replace(strat_target, strat_repl)

    # Test Reports chart
    rep_target = '# ── 5. Test Reports ───────────────────────────────────────────────────────────\nadd_heading(doc, "5. Test Reports", 2)'
    rep_repl = rep_target + '\nadd_diagram(doc, "r5_fig1_test_results.png", "Figure 5.2: Test Execution Results and Pass Rate Breakdown (Report 5)", 6.0)'
    content = content.replace(rep_target, rep_repl)

    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("Patched create_report5.py")

if __name__ == "__main__":
    patch_report3()
    patch_report4()
    patch_report5()
    print("All report scripts patched successfully!")

