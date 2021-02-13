#
# (c) Copyright Ascensio System SIA 2020
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

class FileModel

  attr_accessor :file_name, :mode, :type, :user_ip, :lang, :uid, :uname, :action_data

  def initialize(attributes = {})
    @file_name = attributes[:file_name]
    @mode = attributes[:mode]
    @type = attributes[:type]
    @user_ip = attributes[:user_ip]
    @lang = attributes[:lang]
    @user_id = attributes[:uid]
    @user_name = attributes[:uname]
    @action_data = attributes[:action_data]
  end

  def type
    @type ? @type : "desktop"
  end

  def file_ext
    File.extname(@file_name)
  end

  def file_uri
    DocumentHelper.get_file_uri(@file_name, true)
  end

  def file_uri_user
    DocumentHelper.get_file_uri(@file_name, false)
  end

  def document_type
    FileUtility.get_file_type(@file_name)
  end

  def key
    uri = DocumentHelper.cur_user_host_address(nil) + '/' + @file_name
    stat = File.mtime(DocumentHelper.storage_path(@file_name, nil))
    return ServiceConverter.generate_revision_id("#{uri}.#{stat.to_s}")
  end

  def callback_url
    DocumentHelper.get_callback(@file_name)
  end

  def cur_user_host_address
    DocumentHelper.cur_user_host_address(nil)
  end

  def get_config
    editorsmode = @mode ? @mode : "edit"
    canEdit = DocumentHelper.edited_exts.include?(file_ext)
    mode = canEdit && editorsmode.eql?("view") ? "view" : "edit"

    config = {
      :type => type(),
      :documentType => document_type,
      :document => {
        :title => @file_name,
        :url => file_uri,
        :fileType => file_ext.delete("."),
        :key => key,
        :info => {
          :owner => "Me",
          :uploaded => Time.now.to_s,
          :favorite => @user_id ? @user_id.eql?("uid-2") : nil
        },
        :permissions => {
          :comment => !editorsmode.eql?("view") && !editorsmode.eql?("fillForms") && !editorsmode.eql?("embedded") && !editorsmode.eql?("blockcontent"),
          :download => true,
          :edit => canEdit && (editorsmode.eql?("edit") || editorsmode.eql?("filter") || editorsmode.eql?("blockcontent")),
          :fillForms => !editorsmode.eql?("view") && !editorsmode.eql?("comment") && !editorsmode.eql?("embedded") && !editorsmode.eql?("blockcontent"),
          :modifyFilter => !editorsmode.eql?("filter"),
          :modifyContentControl => !editorsmode.eql?("blockcontent"),
          :review => editorsmode.eql?("edit") || editorsmode.eql?("review")
        }
      },
      :editorConfig => {
        :actionLink => @action_data ? JSON.parse(@action_data) : nil,
        :mode => mode,
        :lang => @lang ? @lang : "en",
        :callbackUrl => callback_url,
        :user => {
          :id => @user_id ? @user_id : "uid-0",
          :name => @user_name ? @user_name : "John Smith"
        },
        :embedded => {
          :saveUrl => file_uri_user,
          :embedUrl => file_uri_user,
          :shareUrl => file_uri_user,
          :toolbarDocked => "top"
        },
        :customization => {
          :forcesave => false
        }
      }
    }

    if JwtHelper.is_enabled
      config["token"] = JwtHelper.encode(config)
    end

    return config
  end

  def get_history
    file_name = @file_name
    file_ext = File.extname(file_name)
    doc_key = key()
    doc_uri = file_uri()

    hist_dir = DocumentHelper.history_dir(DocumentHelper.storage_path(@file_name, nil))
    cur_ver = DocumentHelper.get_file_version(hist_dir)

    if (cur_ver > 0)
      hist = []
      histData = {}

      for i in 1..cur_ver
        obj = {}
        dataObj = {}
        ver_dir = DocumentHelper.version_dir(hist_dir, i)

        cur_key = doc_key
        if (i != cur_ver)
          File.open(File.join(ver_dir, "key.txt"), 'r') do |file|
            cur_key = file.read()
          end
        end
        obj["key"] = cur_key
        obj["version"] = i

        if (i == 1)
          if File.file?(File.join(hist_dir, "createdInfo.json"))
            File.open(File.join(hist_dir, "createdInfo.json"), 'r') do |file|
              cr_info = JSON.parse(file.read())

              obj["created"] = cr_info["created"]
              obj["user"] = {
                :id => cr_info["created"],
                :name => cr_info["name"]
              }
            end
          end
        end

        dataObj["key"] = cur_key
        dataObj["url"] = i == cur_ver ? doc_uri : DocumentHelper.get_path_uri(File.join("#{file_name}-hist", i.to_s, "prev#{file_ext}"))
        dataObj["version"] = i

        if (i > 1)
          changes = nil
          File.open(File.join(DocumentHelper.version_dir(hist_dir, i - 1), "changes.json"), 'r') do |file|
            changes = JSON.parse(file.read())
          end

          change = changes["changes"][0]

          obj["changes"] = changes["changes"]
          obj["serverVersion"] = changes["serverVersion"]
          obj["created"] = change["created"]
          obj["user"] = change["user"]

          prev = histData[(i - 2).to_s]
          dataObj["previous"] = {
            :key => prev["key"],
            :url => prev["url"]
          }

          dataObj["changesUrl"] = DocumentHelper.get_path_uri(File.join("#{file_name}-hist", (i - 1).to_s, "diff.zip"))
        end

        if JwtHelper.is_enabled
          dataObj["token"] = JwtHelper.encode(dataObj)
        end

        hist.push(obj)
        histData[(i - 1).to_s] = dataObj
      end

      return {
        :hist => {
          :currentVersion => cur_ver,
          :history => hist
        },
        :histData => histData
      }
    end

    return nil

  end

  def get_insert_image 
    insert_image = {
      :fileType => "png",
      :url => DocumentHelper.get_server_url(true) + "/assets/logo.png"
    }

    if JwtHelper.is_enabled
      insert_image["token"] = JwtHelper.encode(insert_image)
    end

    return insert_image.to_json.tr("{", "").tr("}","")
  end

  def get_compare_file
    compare_file = {
      :fileType => "docx",
      :url => DocumentHelper.get_server_url(true) + "/assets/sample/sample.docx"
    }

    if JwtHelper.is_enabled
      compare_file["token"] = JwtHelper.encode(compare_file)
    end
    
    return compare_file
  end

  def dataMailMergeRecipients
    dataMailMergeRecipients = {
      :fileType => "csv",
      :url => DocumentHelper.get_server_url(true) + "/csv"
    }

    if JwtHelper.is_enabled
      dataMailMergeRecipients["token"] = JwtHelper.encode(dataMailMergeRecipients)
    end

    return dataMailMergeRecipients
  end

end