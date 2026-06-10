import requests
from bs4 import BeautifulSoup
import subprocess
import json

base_url = "http://localhost:5000"
login_url = f"{base_url}/Login"
list_url = f"{base_url}/Documents/List"

print("--- E2E TEST: QualityDoc PostgreSQL Synchronization ---")

# Start requests session
session = requests.Session()

# 1. Get Login Page & Extract Anti-Forgery Token
print("1. Fetching login page...")
r_get_login = session.get(login_url)
if r_get_login.status_code != 200:
    print(f"FAILED to fetch login page: {r_get_login.status_code}")
    exit(1)

soup = BeautifulSoup(r_get_login.text, "html.parser")
token_input = soup.find("input", {"name": "__RequestVerificationToken"})
if not token_input:
    print("FAILED: Could not find __RequestVerificationToken on login page")
    exit(1)
token = token_input["value"]
print(f"Token found: {token[:10]}...")

# 2. Login
print("2. Logging in...")
login_payload = {
    "Correo": "superadmin@superadmin",
    "Password": "superadmin123",
    "__RequestVerificationToken": token
}
r_post_login = session.post(login_url, data=login_payload, allow_redirects=False)
if r_post_login.status_code not in (301, 302):
    print(f"FAILED to login: HTTP {r_post_login.status_code}")
    # Print error summary from page
    soup_error = BeautifulSoup(r_post_login.text, "html.parser")
    err_div = soup_error.find("div", {"class": "text-red-500"}) or soup_error.find("p", {"class": "text-red-500"})
    if err_div:
        print(f"Server response error: {err_div.text.strip()}")
    else:
        print("Response body preview:")
        print(r_post_login.text[:300])
    exit(1)

redirect_url = r_post_login.headers.get("Location")
print(f"Logged in successfully. Redirecting to: {redirect_url}")

# 3. Get Documents List Page & Extract Anti-Forgery Token for SyncPostgre
print("3. Fetching documents list page...")
r_get_list = session.get(list_url)
if r_get_list.status_code != 200:
    print(f"FAILED to fetch documents list: {r_get_list.status_code}")
    exit(1)

soup_list = BeautifulSoup(r_get_list.text, "html.parser")
# Find the SyncPostgre form
sync_form = soup_list.find("form", {"asp-page-handler": "SyncPostgre"}) or soup_list.find("form", action=lambda x: x and "handler=SyncPostgre" in x)
if not sync_form:
    # Let's find any form with SyncPostgre handler
    forms = soup_list.find_all("form")
    for f in forms:
        action = f.get("action", "")
        if "handler=SyncPostgre" in action or "handler=SyncPostgre" in str(f):
            sync_form = f
            break

if not sync_form:
    print("FAILED: Could not find SyncPostgre form on list page. Checking if document is already synchronized or if status is not Active.")
    # Let's see if there is a '✔ Sincronizado' or '-' or document table
    rows = soup_list.find_all("tr")
    print(f"Found {len(rows)-1} documents in table. Displaying rows:")
    for row in rows[1:]:
        cols = [c.text.strip() for c in row.find_all("td")]
        print(" | ".join(cols))
    
    # We will try to post anyway since we know the document ID
    print("Trying manual POST to SyncPostgre endpoint anyway...")
    # Find any request verification token on list page
    token_input = soup_list.find("input", {"name": "__RequestVerificationToken"})
    token = token_input["value"] if token_input else ""
else:
    token_input = sync_form.find("input", {"name": "__RequestVerificationToken"})
    token = token_input["value"] if token_input else ""

doc_id = "02bbfe29-a756-4e9d-a317-eaa843949430"
print(f"Using document ID: {doc_id}")
print(f"Sync token: {token[:10]}...")

# 4. Post to SyncPostgre page handler
print("4. Triggering PostgreSQL sync handler...")
sync_payload = {
    "id": doc_id,
    "__RequestVerificationToken": token
}
# URL for Razor Page handler is /Documents/List?handler=SyncPostgre
sync_url = f"{list_url}?handler=SyncPostgre"
r_post_sync = session.post(sync_url, data=sync_payload, allow_redirects=False)

print(f"Response Status: {r_post_sync.status_code}")
if r_post_sync.status_code in (301, 302):
    print(f"Redirect Location: {r_post_sync.headers.get('Location')}")
    # Fetch final page to see TempData messages
    r_final = session.get(base_url + r_post_sync.headers.get('Location'))
    soup_final = BeautifulSoup(r_final.text, "html.parser")
    # Check for success/error alerts (classes bg-green-50 or bg-red-50)
    success_alert = soup_final.find("div", class_=lambda x: x and "bg-green-50" in x)
    error_alert = soup_final.find("div", class_=lambda x: x and "bg-red-50" in x)
    if success_alert:
        print(f"Alert Success: {success_alert.text.strip()}")
    if error_alert:
        print(f"Alert Error: {error_alert.text.strip()}")
else:
    print("POST failed. Response preview:")
    print(r_post_sync.text[:500])
    exit(1)

# 5. Query PostgreSQL container directly
print("\n5. Querying PostgreSQL container directly to verify row exists...")
try:
    cmd = ["sudo", "docker", "exec", "-i", "postgres_db", "psql", "-U", "postgres", "-d", "qualitydoc", "-c", f"SELECT id, title, version_number, status_name, synced_at FROM documents WHERE id = '{doc_id}';"]
    output = subprocess.check_output(cmd, text=True)
    print("PostgreSQL Output:")
    print(output)
    if doc_id in output:
        print("SUCCESS: Document successfully synchronized and verified in PostgreSQL!")
    else:
        print("FAILED: Document was not found in PostgreSQL table.")
        exit(1)
except Exception as e:
    print(f"Error querying PostgreSQL: {e}")
    exit(1)
