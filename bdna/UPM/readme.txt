http://documents.software.dell.com/privilege-manager-for-unix/6.0/administrators-guide/

Managing Security Policy->Editing the Policy Interactively:

http://documents.software.dell.com/privilege-manager-for-unix/6.0/administrators-guide/managing-security-policy/editing-the-policy-interactively



[root@RH5-8156 examples_600_027]# /opt/quest/sbin/pmpolicy edit -p profileBasedPolicy.conf
  ** Validate options                                                    [ OK ]
** Edit Policy

  ** Check out working copy                                              [ OK ]
  ** Editing file:profileBasedPolicy.conf
  ** Perform syntax check                                                [ OK ]
  ** Verify files to commit                                              [ OK ]
Please enter the commit log message: updated by cacey.

  ** Commit change from working copy                                     [ OK ]
    ** Committed revision 35
  ** Finished editing policy

[test@RH5-8156 ~]$ pmrun su -
********************************************************************
**      Quest Privilege Manager for Unix Version 6.0.0 (027)      **
********************************************************************
** You are required to authenticate as the UPM user :"test" before running this command
Password:
Request accepted by the "all" profile on server:RH5-8156.RH5-8156

 All interactions with this command will be recorded in the file:
       /var/opt/quest/qpm4u/iolog//all/test/su_20160901_1343_087312

Executing "su" as user "root" ...
********************************************************************************

[root@RH5-8156 ~]#

扫8.156 pmrun的 用 test/<UED>bdna</UED><UPM>bdna</UPM>
root/bdnacn
