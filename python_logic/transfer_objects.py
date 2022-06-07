import os
import argparse
from utils import *

if __name__ == '__main__':
    # Clean a directory of my_local_project/[dashboards, filters, worksheets]/ and overwrite it, rename it, or duplicate a clean version:

    # python3 sfsnowsightextensions/python_logic/transfer_objects.py 
    # --objects' ['filters,dashboards,worksheets']
    # --folder [path to dir with dashboards/, filters/, and/or worksheet/ dirs] 
    # --role [role you'd like the dashboards to be created with (this can be changed individually later)] 
    # --warehouse [warehouse you'd like the dashboards to be use (this can be changed individually later)]  
    # --database [database you'd like the dashboards to use as context with (this can be changed individually later)]  
    # --schema [role you'd like the dashboards to be created with (this can be changed individually later)] 
    # --account [account name you'd like the dashboards to have in their file names, else 'Generic' is used]
    # --overwrite [True or False, overwrite existing file in existing folder]
    # --out_folder [Rename any folder that is output from the results of python script changes/cleaning/etc.]
    # --clean [True or False, clean the file of any metadata]
    # sample: python3 sfsnowsightextensions-wrapper/python_logic/dashboard_transfer.py --objects 'filters,dashboards,worksheets' --folder ./my_local_project --role ACCOUNTADMIN --warehouse COMPUTE_WH --account my_target_account_locator --out_folder my_target_account_folder  
    parser = argparse.ArgumentParser(description='Read in scripts from one dir and output in another or same dir')
    parser.add_argument('--objects', '-o', type=str, help='can take on the values filters,dashboards,worksheets, default is all three', default='filters,dashboards,worksheets')
    parser.add_argument('--folder', '-f', type=str, help='relative path to folder/directory name', default='.')
    parser.add_argument('--role', '-r', type=str, help='role name for target acct')
    parser.add_argument('--warehouse', '-w', type=str, help='warehouse name for target acct')
    parser.add_argument('--database', '-d', type=str, help='database name for target acct', default='')
    parser.add_argument('--schema', '-s', type=str, help='schema name for target acct', default='')
    parser.add_argument('--account', '-a', type=str, help='target account for filenaming', default='')
    parser.add_argument('--overwrite', '-ow', type=bool, help='overwrite files where they are', default=False)
    parser.add_argument('--out_folder', '-of', type=str, help='rename the directory you wish to be output to', default='')
    args = parser.parse_args()
    args = vars(args)

    object_types = [x.lower().strip() for x in args['objects'].split(',')]

    if 'filters' in object_types:
        for file in [x for x in os.listdir(f"{args['folder']}/filters") if '.json' in x.lower()]:
            change_filter_configuration(
                folder=f"{args['folder']}/filters",
                filename=file,
                role=args['role'],
                warehouse=args['warehouse'],
                database=args['database'],
                schema=args['schema'],
                account=args['account'],
                overwrite=args['overwrite'],
                out_folder=args['out_folder'],
                clean = args['clean']
            )

    if 'dashboards' in object_types:
        for file in [x for x in os.listdir(f"{args['folder']}/dashboards") if '.json' in x.lower()]:
            change_dashboard_configuration(
                folder=f"{args['folder']}/dashboards",
                filename=file,
                role=args['role'],
                warehouse=args['warehouse'],
                database=args['database'],
                schema=args['schema'],
                account=args['account'],
                overwrite=args['overwrite'],
                out_folder=args['out_folder'],
                clean = args['clean']
            )

    if 'worksheets' in object_types:
        for file in [x for x in os.listdir(f"{args['folder']}/worksheets") if '.json' in x.lower()]:
            change_worksheet_configuration(
                folder=f"{args['folder']}/worksheets",
                filename=file,
                role=args['role'],
                warehouse=args['warehouse'],
                database=args['database'],
                schema=args['schema'],
                account=args['account'],
                overwrite=args['overwrite'],
                out_folder=args['out_folder'],
                clean = args['clean']
            )
