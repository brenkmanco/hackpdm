import os
import re

def extract_cs_skeleton(root_dir, output_path):
    skeleton = []
    
    # Regex to match structural keywords (ignoring standard logic lines)
    structural_pattern = re.compile(
        r'^\s*(public|private|protected|internal|namespace|class|interface|struct|enum|record)\b'
    )

    for dirpath, _, filenames in os.walk(root_dir):
        for file in filenames:
            if file.endswith('.cs') and not file.endswith('.Designer.cs'):
                filepath = os.path.join(dirpath, file)
                skeleton.append(f"\n// --- File: {os.path.relpath(filepath, root_dir)} ---")
                
                try:
                    with open(filepath, 'r', encoding='utf-8') as f:
                        lines = f.readlines()
                        
                        for line in lines:
                            stripped = line.strip()
                            # Keep attributes, structure declarations, and properties
                            if stripped.startswith('[') or structural_pattern.match(stripped):
                                # If it's a method opening, append an empty body indicator
                                if stripped.endswith('{'):
                                    skeleton.append(line.rstrip() + " ... }")
                                else:
                                    skeleton.append(line.rstrip())
                except Exception as e:
                    skeleton.append(f"// Error reading file: {e}")

    with open(output_path, 'w', encoding='utf-8') as out:
        out.write("\n".join(skeleton))
    
    print(f"Skeleton generated successfully: {output_path}")

if __name__ == "__main__":
    # Set to your current directory
    target_directory = "." 
    output_file = "ArchitectureSkeleton.txt"
    extract_cs_skeleton(target_directory, output_file)