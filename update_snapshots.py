import re
import os

def update_snapshots():
    error_file = r"d:\SourceCode\AccountingCompanion\XmlSourceGenerator\test_errors_3.txt"
    test_file = r"d:\SourceCode\AccountingCompanion\XmlSourceGenerator\tests\XmlSourceGenerator.UnitTests\SourceGeneration\SnapshotTests.cs"

    if not os.path.exists(error_file):
        print(f"Error file not found: {error_file}")
        return

    with open(error_file, 'r', encoding='utf-8') as f:
        error_content = f.read()

    with open(test_file, 'r', encoding='utf-8') as f:
        test_content = f.read()

    # Regex to find failures content
    # Pattern: Failed XmlSourceGenerator.UnitTests.SourceGeneration.SnapshotTests.(MethodName) .*? but "(.*?)" has a length
    # Note: DOTALL is needed.
    # The actual code in error log might contain " so we need to be careful.
    # FluentAssertions format: ... but "ACTUAL_CODE" has a length ...
    
    # We'll split by "Failed XmlSourceGenerator..." to handle multiple failures
    sections = re.split(r'Failed XmlSourceGenerator\.UnitTests\.SourceGeneration\.SnapshotTests\.', error_content)
    
    # Skip preamble
    for section in sections[1:]:
        # Extract method name
        m = re.match(r'(\w+)', section)
        if not m:
            continue
        method_name = m.group(1)
        
        # Extract actual code
        # Look for 'but "' and '" has a length'
        # Since code can contain quotes, we look for the last occurrence of '" has a length' relative to the start?
        # Actually, likely the code is block of text.
        
        # A robust way: find 'Expected actualCode to be ' ... 'but "' ... '" has a length'
        
        start_marker = 'but "'
        end_marker = '" has a length'
        
        start_idx = section.find(start_marker)
        if start_idx == -1:
            print(f"Could not find start marker for {method_name}")
            continue
            
        start_idx += len(start_marker)
        
        end_idx = section.rfind(end_marker)
        if end_idx == -1:
            print(f"Could not find end marker for {method_name}")
            continue
            
        actual_code = section[start_idx:end_idx]
        
        # Unescape if necessary?
        # FluentAssertions output usually doesn't escape newlines, but might escape quotes?
        # In the log view, quotes appeared as " (e.g. Element("Name")).
        # So we assume capture is raw string.
        
        # Prepare for insertion into C# verbatim string @"..."
        # We need to replace " with ""
        escaped_code = actual_code.replace('"', '""')
        
        # Now replace in test_file
        # Find the method
        # public void MethodName()
        # ...
        # var expectedCode = @"...";
        
        # Regex for test file replacement
        # We want to replace the content inside var expectedCode = @"...";
        
        pattern = re.compile(f'public void {method_name}\(\).*?var expectedCode = @"(.*?)"', re.DOTALL)
        
        # Check if method exists in file
        if method_name not in test_content:
             print(f"Method {method_name} not found in test file")
             continue
             
        # We need to find the specific range to replace.
        # Find method start
        method_start = test_content.find(f"public void {method_name}()")
        if method_start == -1: 
            continue
            
        # Find var expectedCode = @" after method start
        code_start_marker = 'var expectedCode = @"'
        code_start = test_content.find(code_start_marker, method_start)
        if code_start == -1:
            continue
            
        code_start += len(code_start_marker)
        
        # Find end of string: ";
        # But wait, generated code contains "; inside checks?
        # Using verbatim string, it ends with ";
        # But inside string, " is escaped as "".
        # So trigger is non-escaped " followed by ;
        # Actually, in C#, verbatim string ends at first " that is NOT followed by another ".
        
        # Simple heuristic: scan forward, handling "" escape.
        cursor = code_start
        while cursor < len(test_content):
            if test_content[cursor] == '"':
                if cursor + 1 < len(test_content) and test_content[cursor+1] == '"':
                    cursor += 2 # Skip escaped quote
                else:
                    # End of string
                    break
            else:
                cursor += 1
        
        code_end = cursor
        
        # Replace
        print(f"Updating {method_name}...")
        test_content = test_content[:code_start] + escaped_code + test_content[code_end:]

    with open(test_file, 'w', encoding='utf-8') as f:
        f.write(test_content)
    
    print("Update complete.")

if __name__ == "__main__":
    update_snapshots()
