import sys
import os
import json
from pathlib import Path


def clean_filename(filename, account=''):
    filename_parts = filename.split('.')
    if account:
        filename_parts[1] = account
    else: 
        filename_parts[1] = 'Generic'
    if 'dashboard' in [x.lower() for x in filename_parts] and len(filename_parts) > 4:
        del filename_parts[-2] # Getting rid of the unique id if possible
    filename = '.'.join(filename_parts)
    return filename


def clean_foldername(foldername, out_folder=''):
    foldername_parts = foldername.split('/')
    foldername_parts[-2] = out_folder
    foldername = '/'.join(foldername_parts)
    return foldername


def write_file(folder, filename, data, account='', overwrite=False, out_folder='', clean=True):
    if overwrite:
        cleaned_filename = filename
        cleaned_foldername = folder
    else:
        if clean:
            cleaned_filename = clean_filename(filename, account)
            cleaned_foldername = clean_foldername(folder, out_folder)
        else:
            cleaned_filename = filename
            cleaned_foldername = out_folder

    base = Path(cleaned_foldername)
    base.mkdir(parents=True, exist_ok=True)
    
    clean_json_recursive(data, ['FileSystemSafeName'], replacement_vals={'FileSystemSafeName':cleaned_filename})
        
    fn = f"{cleaned_foldername}/{cleaned_filename}"
    if overwrite:
        print(f"Overwriting file {fn}")
    else:
        print(f"Creating file {fn}")
    
    with open(fn, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=4)


def clean_json_recursive(json_input, lookup_keys, replacement_vals={}):
    if isinstance(json_input, dict):
        for k, v in json_input.items():
            if type(v) in [dict, list]:
                if json_input[k]:
                    clean_json_recursive(json_input[k], lookup_keys, replacement_vals)
            elif k in lookup_keys:
                if k in replacement_vals:
                    json_input[k] = replacement_vals[k]
                else:
                    json_input[k] = '' # Hardcoding to string here, might be best to preserve type.
                
    elif isinstance(json_input, list):
        for item in json_input:
            clean_json_recursive(item, lookup_keys, replacement_vals)


def change_dashboard_configuration(folder, filename, role, warehouse, database="", schema="", account="", overwrite=False, out_folder='', clean=True):
    # filters = [] # For use in pulling those filters down which are present in dashboards.
    fields_to_clean = ['Role','Warehouse','Database','Schema','OwnerUserID','OwnerUserName','URL','DashboardID','AccountName','AccountFullName','AccountUrl','OrganizationID','Region']
    with open(f"{folder}/{filename}", 'r') as f:
        out_json = json.load(f)
        
        if clean:
            clean_json_recursive(out_json, fields_to_clean, {'Role': role, 'Warehouse': warehouse, 'Database': database, 'Schema': schema})

    write_file(folder, filename, out_json, account, overwrite, out_folder)


def change_filter_configuration(folder, filename, role, warehouse, database="", schema="", account="", overwrite=False, out_folder='', clean=True):
    fields_to_clean = ['Role','Warehouse','Database','Schema','role','warehouse','database','schema','OwnerUserID','OwnerUserName','URL','DashboardID','AccountName','AccountFullName','AccountUrl','OrganizationID','Region']
    with open(f"{folder}/{filename}", 'r') as f:
        out_json = json.load(f)
        if clean:
            clean_json_recursive(out_json, fields_to_clean, {'Role': role, 'Warehouse': warehouse, 'Database': database, 'Schema': schema, 'role': role, 'warehouse': warehouse, 'database': database, 'schema': schema})

    write_file(folder, filename, out_json, account, overwrite, out_folder)


def change_worksheet_configuration(folder, filename, role, warehouse, database="", schema="", account="", overwrite=False, out_folder='', clean=True):
    fields_to_clean = ['FolderID', 'FolderName', 'Role','Warehouse','Database','Schema', 'OwnerUserID','OwnerUserName','URL','DashboardID','AccountName','AccountFullName','AccountUrl','OrganizationID','Region']
    with open(f"{folder}/{filename}", 'r') as f:
        out_json = json.load(f)
        if clean:
            clean_json_recursive(out_json, fields_to_clean, {'Role': role, 'Warehouse': warehouse, 'Database': database, 'Schema': schema})
    
    write_file(folder, filename, out_json, account, overwrite, out_folder)


def script_to_worksheet(folder, filename, role, warehouse, database="", schema="", account="", overwrite=False, out_folder='', clean=False):
    filename_no_extension = filename.split('.sql')[0]
    filename_no_extension = f"Worksheet.{filename_no_extension}.{account if account else 'Generic'}"
    worksheet = {
            "FolderID": "",
            "FolderName": "",
            "OwnerUserID": "",
            "OwnerUserName": "",
            "Version": 20,
            "URL": "",
            "WorksheetID": "",
            "WorksheetName": "",
            "StartedUtc": "0001-01-01T00:00:00.0000000Z",
            "EndedUtc": "0001-01-01T00:00:00.0000000Z",
            "ModifiedUtc": "0001-01-01T00:00:00.0000000Z",
            "Role": role,
            "Warehouse": warehouse,
            "Database": database,
            "Schema": schema,
            "Query": "",
            "Parameters": [],
            "Charts": [],
            "FileSystemSafeName": "",
            "_CreatedWith": "Snowflake Snowsight Extensions",
            "_CreatedVersion": "2022.6.2.0",
            "AccountName": "",
            "AccountFullName": "",
            "AccountUrl": "",
            "OrganizationID": "",
            "Region": ""
            }

    with open(f'{folder}/{filename}','r') as f:
        query = f.read()

    worksheet['Query'] = query
    write_file(folder, filename, worksheet, account, overwrite, out_folder, clean=clean)
