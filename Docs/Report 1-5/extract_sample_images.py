import docx, os

def extract_images_from_docx(docx_path, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    doc = docx.Document(docx_path)
    count = 0
    base = os.path.splitext(os.path.basename(docx_path))[0]
    for rel in doc.part.rels.values():
        if "image" in rel.target_ref:
            img_part = rel.target_part
            ext = img_part.content_type.split("/")[-1]
            if ext == "x-emf":
                ext = "emf"
            elif ext == "jpeg":
                ext = "jpg"
            fname = f"{base}_img_{count}.{ext}"
            fpath = os.path.join(out_dir, fname)
            with open(fpath, "wb") as f:
                f.write(img_part.blob)
            print(f"Extracted: {fname} ({len(img_part.blob)} bytes)")
            count += 1

out_dir = r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\diagrams"
extract_images_from_docx(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\Bản gốc\Report1_Project Introduction.docx", out_dir)
extract_images_from_docx(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\Bản gốc\Report3_Software Requirement Specification.docx", out_dir)
extract_images_from_docx(r"C:\Users\Admin\Hold My Hand\Docs\Report 1-5\Bản gốc\Report4_Software Design Document.docx", out_dir)

