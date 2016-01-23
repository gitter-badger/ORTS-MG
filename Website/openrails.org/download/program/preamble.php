          <!-- Modal -->
<?php          
echo("<div id='$modal' class='modal fade' tabindex='-1' role='dialog' aria-labelledby='myModalLabel' aria-hidden='true'>");
?>
            <div class="modal-dialog">
              <div class="modal-content">
                <div class="modal-header">
                  <button type="button" class="close" data-dismiss="modal" aria-hidden="true">&times;</button>
<?php
echo("<h2 class='modal-title'>$title</h2>");
?>
                </div>
                <div class="modal-body">
                  <p>
                    Please note: the Open Rails downloads do not include any content - routes, rolling stock, activities - just the
                    simulation program. 
                  </p><p>
                    If you have content suitable for Open Rails or Microsoft Train Simulator already in place, then you can use the
                    Open Rails program to operate those routes and drive those locomotives straight away.
                  </p><p>
                    If not, then you will have to install some models <a href='http://openrails.org/trade/'>bought from a vendor</a> or 
                    <a href='http://openrails.org/share/community/'>free from the community</a> before you can use Open Rails.
                  </p><p>
                    Or, you can try out our free <a href="/download/content">Demo Model 1 content</a>. It's a large download
                    (250MB) but this will get you driving straight away.
                  </p><p class="text-right">
<?php 
echo ("<a href='/download/program/confirm.php?file=$download_file' class='btn download_button'>Download</a>");
?>
                    <button type="button" data-dismiss="modal" aria-hidden="true" class="btn btn-default cancel_button">Cancel</button>
                  </p>
                </div><!-- End of Modal body -->
              </div><!-- End of Modal content -->
            </div><!-- End of Modal dialog -->
          </div><!-- End of Modal -->
