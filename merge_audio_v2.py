#!/usr/bin/env python3
"""
Extract BackgroundMusic and AudioSource objects from Sapuni and add to main scene.
"""

import subprocess
import re

def get_file_from_git(branch, filepath):
    """Get file content from a specific git branch."""
    result = subprocess.run(
        ['git', 'show', f'{branch}:{filepath}'],
        capture_output=True,
        text=True,
        cwd='.'
    )
    if result.returncode != 0:
        raise Exception(f"Failed: {result.stderr}")
    return result.stdout

print("Reading scenes...")
main_scene = get_file_from_git('HEAD', 'Assets/Scenes/SampleScene.unity')
sapuni_scene = get_file_from_git('Sapuni', 'Assets/Scenes/SampleScene.unity')

# Find the BackgroundMusic GameObject in Sapuni by finding the complete section
# Split into lines for easier processing
sapuni_lines = sapuni_scene.split('\n')

# Find lines related to BackgroundMusic and AudioSource
audio_objects_to_add = []
i = 0
while i < len(sapuni_lines):
    line = sapuni_lines[i]
    
    # Look for AudioSource components and GameObject definitions for audio
    if ('--- !u!1 &' in line or '--- !u!82 &' in line) and i < len(sapuni_lines) - 50:
        # Check if this section is for audio (BackgroundMusic, AudioSource, etc.)
        section_text = '\n'.join(sapuni_lines[i:min(i+100, len(sapuni_lines))])
        if 'BackgroundMusic' in section_text or 'AudioSource' in section_text:
            # Collect the complete object (until next '--- !u!' marker)
            start = i
            end = i + 1
            while end < len(sapuni_lines) and not sapuni_lines[end].startswith('--- !u!'):
                end += 1
            
            # Check if this is actually for audio
            obj_section = '\n'.join(sapuni_lines[start:end])
            if 'BackgroundMusic' in obj_section or ('AudioSource' in obj_section and 'Component' not in sapuni_lines[i]):
                audio_objects_to_add.append('\n'.join(sapuni_lines[start:end]))
                i = end
                continue
    i += 1

print(f"Found {len(audio_objects_to_add)} audio objects")

# Find the insertion point in main scene (before the last metadata section)
main_lines = main_scene.split('\n')

# Find the last --- !u! marker (last GameObject)
last_object_idx = -1
for i in range(len(main_lines) - 1, -1, -1):
    if main_lines[i].startswith('--- !u!'):
        last_object_idx = i
        break

print(f"Inserting at line {last_object_idx}")

# Rebuild with audio objects
if last_object_idx > 0:
    # Build new scene
    new_content_parts = [
        '\n'.join(main_lines[:last_object_idx])  # Everything before last object
    ]
    
    # Add all audio objects
    for obj in audio_objects_to_add:
        if obj.strip():
            new_content_parts.append(obj)
    
    # Add the rest
    new_content_parts.append('\n'.join(main_lines[last_object_idx:]))
    
    new_content = '\n'.join(new_content_parts)
    
    # Write back
    with open('Assets/Scenes/SampleScene.unity', 'w', encoding='utf-8') as f:
        f.write(new_content)
    
    print("Merge complete!")
    
    # Verify
    with open('Assets/Scenes/SampleScene.unity', 'r', encoding='utf-8') as f:
        merged = f.read()
        if 'BackgroundMusic' in merged:
            print("✓ BackgroundMusic found in merged scene")
        if 'AudioSource' in merged:
            print("✓ AudioSource components found in merged scene")
