# -*- coding: utf-8 -*-
"""
Script to embed all diagrams and FPT logos into Report 3, 4, and 5.
"""
import os
import sys
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH

DIAGRAM_DIR = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"

def add_diagram(doc, img_name, caption, width_inches=6.0):
    img_path = os.path.join(DIAGRAM_DIR, img_name)
    if not os.path.exists(img_path):
        print(f"Warning: Image not found: {img_path}")
        return None
    
    p_img = doc.add_paragraph()
    p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_img.paragraph_format.space_before = Pt(8)
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
    return p_cap

def add_cover_logo(doc, logo_name="fpt_logo.png", width_inches=2.2):
    logo_path = os.path.join(DIAGRAM_DIR, logo_name)
    if os.path.exists(logo_path):
        # Insert at the very first paragraph
        p = doc.paragraphs[0] if doc.paragraphs else doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_after = Pt(12)
        r = p.add_run()
        r.add_picture(logo_path, width=Inches(width_inches))
        return True
    return False

print("Helper functions defined successfully.")

