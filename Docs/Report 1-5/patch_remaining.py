# -*- coding: utf-8 -*-
import os

BASE_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5"

# Patch create_report3.py
with open(os.path.join(BASE_DIR, "create_report3.py"), "r", encoding="utf-8") as f:
    r3 = f.read()

# 1. Context Diagram
target_ctx = 'r = p.add_run("Context Diagram:")'
repl_ctx = 'r = p.add_run("Context Diagram:")\n    add_diagram(doc, "r3_fig1_context_diagram.png", "Figure 3.1: Hold My Hand System Context Diagram", 6.0)'
if "r3_fig1_context_diagram.png" not in r3:
    r3 = r3.replace(target_ctx, repl_ctx)
    print("Added Context Diagram to R3")

# 2. Screens Flow Diagram
target_flow = 'add_heading4(doc, "3.1.1 Screens Flow")'
repl_flow = 'add_heading4(doc, "3.1.1 Screens Flow")\n    add_diagram(doc, "r3_fig3_screen_flow.png", "Figure 3.3: Hold My Hand Screens Navigation Flow (Game Client & Web Portal)", 6.2)'
if "r3_fig3_screen_flow.png" not in r3:
    r3 = r3.replace(target_flow, repl_flow)
    print("Added Screens Flow Diagram to R3")

with open(os.path.join(BASE_DIR, "create_report3.py"), "w", encoding="utf-8") as f:
    f.write(r3)

# Patch create_report4.py
with open(os.path.join(BASE_DIR, "create_report4.py"), "r", encoding="utf-8") as f:
    r4 = f.read()

# 1. Logo on cover
target_logo = 'add_cover_line(doc, "CAPSTONE PROJECT REPORT", 28, bold=True, color=DARK_RED)'
repl_logo = """logo_path = os.path.join(r"C:\\Users\\Admin\\Hold My Hand\\Docs\\Report 1-5\\diagrams", "fpt_logo.png")
    if os.path.exists(logo_path):
        p_logo = doc.add_paragraph()
        p_logo.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_logo.paragraph_format.space_after = Pt(10)
        p_logo.add_run().add_picture(logo_path, width=Inches(2.2))
    add_cover_line(doc, "CAPSTONE PROJECT REPORT", 28, bold=True, color=DARK_RED)"""
if "fpt_logo.png" not in r4:
    r4 = r4.replace(target_logo, repl_logo)
    print("Added Logo to R4")

# 2. Architecture Diagram
target_arch = 'add_heading(doc, "1.1 System Architecture", 3)'
repl_arch = 'add_heading(doc, "1.1 System Architecture", 3)\n    add_diagram(doc, "r4_fig1_system_architecture.png", "Figure 4.1: Hold My Hand Multi-Tier Distributed Software Architecture", 6.2)'
if "r4_fig1_system_architecture.png" not in r4:
    r4 = r4.replace(target_arch, repl_arch)
    print("Added Architecture Diagram to R4")

# 3. Package Diagram
target_pkg = 'add_heading(doc, "1.2 Package Diagram", 3)'
repl_pkg = 'add_heading(doc, "1.2 Package Diagram", 3)\n    add_diagram(doc, "r4_fig2_package_diagram.png", "Figure 4.2: Hold My Hand Software Package Structure Diagram", 6.0)'
if "r4_fig2_package_diagram.png" not in r4:
    r4 = r4.replace(target_pkg, repl_pkg)
    print("Added Package Diagram to R4")

# 4. DB Schema Diagram
target_db = 'add_heading(doc, "2. Database Design", 2)'
repl_db = 'add_heading(doc, "2. Database Design", 2)\n    add_diagram(doc, "r4_fig3_db_schema.png", "Figure 4.3: Hold My Hand Database Schema & Redis Cache Structure Diagram", 6.0)'
if "r4_fig3_db_schema.png" not in r4:
    r4 = r4.replace(target_db, repl_db)
    print("Added DB Schema Diagram to R4")

with open(os.path.join(BASE_DIR, "create_report4.py"), "w", encoding="utf-8") as f:
    f.write(r4)

print("Remaining patches applied successfully!")

